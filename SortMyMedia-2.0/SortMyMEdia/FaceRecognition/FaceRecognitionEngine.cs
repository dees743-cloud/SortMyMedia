using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SortMyMedia.FaceRecognition
{
    public sealed class Cluster
    {
        public int ClusterId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string RepresentativeImagePath { get; set; } = string.Empty;
        public List<string> FaceImagePaths { get; set; } = new();
        public List<ClusterFace> Faces { get; set; } = new();

        [JsonIgnore]
        public Image? RepresentativeImage { get; set; }
    }

    public sealed class ClusterFace
    {
        public string ImagePath { get; set; } = string.Empty;
        public string FaceId { get; set; } = string.Empty;
        public Rectangle CropRectangle { get; set; }

        [JsonIgnore]
        public Image? FullImage { get; set; }

        [JsonIgnore]
        public Image? Thumbnail { get; set; }
    }

    public class FaceRecognitionEngine : IDisposable
    {
        private readonly FaceImageLoader loader;
        private readonly FaceDetectorService detector;
        private readonly FaceAlignmentService aligner;
        private readonly FaceEmbeddingService embedder;
        private readonly FaceClusteringService clusterer;
        private readonly PersonRegistry personRegistry;
        private readonly PersonResolver personResolver;
        private readonly AmbiguityResolver ambiguityResolver;
        private readonly IPersonInteractionService personInteractionService;

        public event Action<int, int>? OnChunkProgress;
        public event Action<int, int>? OnGlobalMergeProgress;

        public FaceRecognitionPipelineOptions Options { get; }

        public FaceRecognitionEngine(FaceRecognitionPipelineOptions? options = null)
            : this(options, new PersonNamingService())
        {
        }

        public FaceRecognitionEngine(FaceRecognitionPipelineOptions? options, IPersonInteractionService interactionService)
        {
            Options = (options ?? new FaceRecognitionPipelineOptions()).CloneValidated();
            loader = new FaceImageLoader();
            detector = new FaceDetectorService(80, Options.ScrfdInputSize, Options.EnableDetailedLogging);
            aligner = new FaceAlignmentService(Options.MinAlignedSharpness, Options.EnablePoseQualityCheck, Options.MaxEyeTiltRatio, Options.MinEyeDistanceRatio);
            embedder = new FaceEmbeddingService(Options);
            clusterer = new FaceClusteringService(Options.ClusterDistanceThreshold, Options.MinClusterSize, Options.EnableDetailedLogging);
            personRegistry = new PersonRegistry();
            personResolver = new PersonResolver(Options.ResolverStrictDistanceThreshold, Options.ResolverLooseDistanceThreshold, Options.ResolverMaxAmbiguousCandidates);
            personInteractionService = interactionService;
            ambiguityResolver = new AmbiguityResolver(interactionService);

            clusterer.OnChunkProgress += (completed, total) => OnChunkProgress?.Invoke(completed, total);
            clusterer.OnGlobalMergeProgress += (completed, total) => OnGlobalMergeProgress?.Invoke(completed, total);

            if (Options.EnableDetailedLogging)
                Console.WriteLine($"Face pipeline: clusterDistanceThreshold={Options.ClusterDistanceThreshold:F3}, minClusterSize={Options.MinClusterSize}");
        }

        public DetectionResult[] DetectFaces(string imagePath)
        {
            using Mat img = LoadImage(imagePath);
            if (img.Empty())
                return Array.Empty<DetectionResult>();

            return detector.Detect(img);
        }

        public Mat LoadImage(string imagePath)
        {
            return loader.Load(imagePath);
        }

        public bool IsDetectionQualityGood(DetectionResult det)
        {
            return detector.IsDetectionQualityGood(det);
        }

        public Mat ProcessFace(Mat original, DetectionResult det)
        {
            return aligner.AlignFace(original, det);
        }

        public bool IsAlignedFaceQualityGood(Mat aligned)
        {
            return aligner.IsAlignedQualityGood(aligned);
        }

        public double GetAlignedSharpness(Mat aligned)
        {
            return aligner.GetSharpness(aligned);
        }

        public float[] GetEmbedding(Mat aligned)
        {
            return embedder.GetEmbedding(aligned);
        }

        public float ComputeQualityWeight(float detectionScore, double sharpness)
        {
            return embedder.ComputeQualityWeight(detectionScore, sharpness);
        }

        public List<List<int>> ClusterEmbeddings(List<float[]> embeddings, List<float> weights)
        {
            return clusterer.Cluster(embeddings, weights);
        }

        public async Task ProcessPersonModeAsync(
            IReadOnlyList<string> files,
            string outputFolder,
            Action<string>? log = null,
            Action<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (files.Count == 0)
                return;

            Directory.CreateDirectory(outputFolder);
            string tempThumbnailRoot = Path.Combine(outputFolder, "temp_thumbnails");
            Directory.CreateDirectory(tempThumbnailRoot);
            string tempThumbnailFolder = Path.Combine(tempThumbnailRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempThumbnailFolder);

            try
            {
                var totalTimer = Stopwatch.StartNew();
                long cpuPreprocessTicks = 0;
                long gpuEmbedTicks = 0;

            var allEmbeddings = new List<float[]>();
            var allQualityWeights = new List<float>();
            var allOriginalPaths = new List<string>();
            var allAlignedPaths = new List<string>();

            int processedImages = 0;
            int totalFacesPrepared = 0;

            var paths = Channel.CreateBounded<string>(new BoundedChannelOptions(Math.Max(32, Options.CpuPreprocessWorkers * 4))
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
            });

            var loaded = Channel.CreateBounded<LoadedImageItem>(new BoundedChannelOptions(Options.LoadedImageQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

            var aligned = Channel.CreateBounded<AlignedFaceItem>(new BoundedChannelOptions(Options.AlignedFaceQueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            var embedded = Channel.CreateUnbounded<EmbeddedFaceItem>();

            Task writerTask = Task.Run(async () =>
            {
                try
                {
                    foreach (string file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await paths.Writer.WriteAsync(file, cancellationToken);
                    }
                }
                finally
                {
                    paths.Writer.Complete();
                }
            }, cancellationToken);

            var loadWorkers = new Task[Options.CpuPreprocessWorkers];
            for (int i = 0; i < loadWorkers.Length; i++)
            {
                loadWorkers[i] = Task.Run(async () =>
                {
                    await foreach (string path in paths.Reader.ReadAllAsync(cancellationToken))
                    {
                        Mat img = loader.Load(path);
                        if (img.Empty())
                        {
                            img.Dispose();
                            log?.Invoke($"  → Kon afbeelding niet laden: {path}");
                            continue;
                        }

                        await loaded.Writer.WriteAsync(new LoadedImageItem(path, img), cancellationToken);
                    }
                }, cancellationToken);
            }

            Task loadedCompletionTask = Task.Run(async () =>
            {
                try
                {
                    await Task.WhenAll(loadWorkers);
                    await writerTask;
                    loaded.Writer.Complete();
                }
                catch (Exception ex)
                {
                    loaded.Writer.Complete(ex);
                }
            }, cancellationToken);

            Task detectionTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (LoadedImageItem item in loaded.Reader.ReadAllAsync(cancellationToken))
                    {
                        var cpuStageTimer = Stopwatch.StartNew();
                        try
                        {
                            var detections = detector.Detect(item.Image);
                            log?.Invoke($"Detected {detections.Length} face(s) in {Path.GetFileName(item.Path)}");

                            if (detections.Length == 0)
                                continue;

                            int faceIndex = 1;
                            foreach (var det in detections)
                            {
                                if (det.Score < Options.MinDetectionScore)
                                {
                                    faceIndex++;
                                    continue;
                                }

                                if (!detector.IsDetectionQualityGood(det))
                                {
                                    faceIndex++;
                                    continue;
                                }

                                if (!aligner.IsPoseQualityGood(det))
                                {
                                    faceIndex++;
                                    continue;
                                }

                                Mat alignedFace = aligner.AlignFace(item.Image, det);
                                if (alignedFace.Empty())
                                {
                                    alignedFace.Dispose();
                                    faceIndex++;
                                    continue;
                                }

                                if (!aligner.IsAlignedQualityGood(alignedFace))
                                {
                                    alignedFace.Dispose();
                                    faceIndex++;
                                    continue;
                                }

                                if (!aligner.IsLikelyFaceByColor(alignedFace))
                                {
                                    alignedFace.Dispose();
                                    faceIndex++;
                                    continue;
                                }

                                double sharpness = aligner.GetSharpness(alignedFace);
                                string alignedPath = Path.Combine(tempThumbnailFolder, $"{Path.GetFileNameWithoutExtension(item.Path)}_face{faceIndex}.jpg");
                                Cv2.ImWrite(alignedPath, alignedFace);

                                await aligned.Writer.WriteAsync(new AlignedFaceItem(item.Path, alignedPath, alignedFace, det.Score, sharpness), cancellationToken);
                                Interlocked.Increment(ref totalFacesPrepared);
                                faceIndex++;
                            }
                        }
                        finally
                        {
                            cpuStageTimer.Stop();
                            Interlocked.Add(ref cpuPreprocessTicks, cpuStageTimer.ElapsedTicks);
                            item.Image.Dispose();

                            int done = Interlocked.Increment(ref processedImages);
                            int pct = (int)(done / (double)files.Count * 100);
                            progress?.Invoke(Math.Clamp(pct, 0, 100));
                        }
                    }

                    log?.Invoke("face detection completed. building clusters… Please WAIT!");
                }
                finally
                {
                    aligned.Writer.Complete();
                }
            }, cancellationToken);

            Task embeddingTask = Task.Run(async () =>
            {
                var batch = new List<AlignedFaceItem>(Options.EmbeddingBatchSize);

                async Task FlushBatchAsync()
                {
                    if (batch.Count == 0)
                        return;

                    var mats = new Mat[batch.Count];
                    for (int i = 0; i < batch.Count; i++)
                        mats[i] = batch[i].AlignedFace;

                    try
                    {
                        var gpuTimer = Stopwatch.StartNew();
                        var embeddings = embedder.GetEmbeddings(mats, Options.EnableBatching, Options.EmbeddingBatchSize, Options.EnableDetailedLogging);
                        gpuTimer.Stop();
                        Interlocked.Add(ref gpuEmbedTicks, gpuTimer.ElapsedTicks);

                        for (int i = 0; i < batch.Count; i++)
                        {
                            var emb = i < embeddings.Count ? embeddings[i] : Array.Empty<float>();
                            if (emb.Length == 0)
                                continue;

                            float qualityWeight = embedder.ComputeQualityWeight(batch[i].DetectionScore, batch[i].Sharpness);
                            await embedded.Writer.WriteAsync(new EmbeddedFaceItem(batch[i].OriginalPath, batch[i].AlignedPath, emb, qualityWeight), cancellationToken);
                        }

                        if (Options.EnableDetailedLogging)
                            log?.Invoke($"Embedding batch processed: {batch.Count} faces");
                    }
                    finally
                    {
                        foreach (var item in batch)
                            item.AlignedFace.Dispose();

                        batch.Clear();
                    }
                }

                try
                {
                    await foreach (AlignedFaceItem face in aligned.Reader.ReadAllAsync(cancellationToken))
                    {
                        batch.Add(face);
                        if (batch.Count >= Options.EmbeddingBatchSize)
                            await FlushBatchAsync();
                    }

                    await FlushBatchAsync();
                }
                finally
                {
                    embedded.Writer.Complete();
                }
            }, cancellationToken);

            Task collectorTask = Task.Run(async () =>
            {
                await foreach (EmbeddedFaceItem item in embedded.Reader.ReadAllAsync(cancellationToken))
                {
                    allEmbeddings.Add(item.Embedding);
                    allQualityWeights.Add(item.QualityWeight);
                    allOriginalPaths.Add(item.OriginalPath);
                    allAlignedPaths.Add(item.AlignedPath);
                }
            }, cancellationToken);

            await Task.WhenAll(loadedCompletionTask, detectionTask, embeddingTask, collectorTask);

            log?.Invoke("Clustering…");
            var clusterAssignments = await Task.Run(() => clusterer.Cluster(allEmbeddings, allQualityWeights), cancellationToken);
            var orderedClusterAssignments = SortClusterMembersByCentroidDistance(clusterAssignments, allEmbeddings);

            var overviewService = new FaceOverviewGroupingService();
            FaceOverviewResult overviewResult = overviewService.Run(orderedClusterAssignments, allAlignedPaths, personRegistry.GetKnownNames());

            var namedClusters = new List<(int clusterIndex, FaceOverviewCluster cluster, PersonProfile person)>(overviewResult.Clusters.Count);
            for (int clusterIndex = 0; clusterIndex < overviewResult.Clusters.Count; clusterIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FaceOverviewCluster finalizedCluster = overviewResult.Clusters[clusterIndex];

                PersonProfile person = personRegistry.GetOrCreatePerson(finalizedCluster.Name);
                foreach (int idx in finalizedCluster.FaceIndices)
                    personRegistry.AddEmbedding(person, allEmbeddings[idx], allQualityWeights[idx]);

                namedClusters.Add((clusterIndex, finalizedCluster, person));
                log?.Invoke($"Assigned name '{person.Name}' to cluster {clusterIndex + 1} ({finalizedCluster.FaceIndices.Count} faces)");
            }

            foreach (var namedCluster in namedClusters)
            {
                var cluster = namedCluster.cluster.FaceIndices;
                var person = namedCluster.person;
                string folderName = SanitizeFolderName(string.IsNullOrWhiteSpace(person.Name) ? $"Person_{person.Id:00}" : person.Name);
                string personDir = Path.Combine(outputFolder, folderName);
                Directory.CreateDirectory(personDir);

                foreach (int idx in cluster)
                {
                    string src = allOriginalPaths[idx];
                    string dst = Path.Combine(personDir, Path.GetFileName(src));
                    if (!File.Exists(dst))
                        File.Copy(src, dst);
                }

                log?.Invoke($"{person.Name}: {cluster.Count} face(s)");
            }

            totalTimer.Stop();

            double seconds = Math.Max(0.001, totalTimer.Elapsed.TotalSeconds);
            double throughput = allEmbeddings.Count / seconds;
            double cpuMs = TimeSpan.FromTicks(cpuPreprocessTicks).TotalMilliseconds;
            double gpuMs = TimeSpan.FromTicks(gpuEmbedTicks).TotalMilliseconds;

            if (Options.EnableDetailedLogging)
            {
                log?.Invoke($"Provider info logged by detector/embedder at startup.");
                log?.Invoke($"CPU preprocessing total: {cpuMs:F1} ms");
                log?.Invoke($"GPU embedding total: {gpuMs:F1} ms");
                log?.Invoke($"Faces prepared: {totalFacesPrepared}");
            }

            log?.Invoke($"Throughput: {throughput:F2} faces/s ({allEmbeddings.Count} faces in {seconds:F2}s)");
            progress?.Invoke(100);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempThumbnailFolder))
                        Directory.Delete(tempThumbnailFolder, recursive: true);

                    if (Directory.Exists(tempThumbnailRoot) && !Directory.EnumerateFileSystemEntries(tempThumbnailRoot).Any())
                        Directory.Delete(tempThumbnailRoot, recursive: true);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Kon tijdelijke thumbnailmap niet verwijderen: {ex.Message}");
                }
            }
        }

        public void ProcessImage(string imagePath)
        {
            using Mat img = loader.Load(imagePath);
            if (img.Empty())
            {
                Console.WriteLine("Image load failed.");
                return;
            }

            var detections = detector.Detect(img);
            if (detections.Length == 0)
            {
                Console.WriteLine("No faces found.");
                return;
            }

            List<float[]> embeddings = new();
            List<float> weights = new();
            List<Mat> alignedFaces = new();

            foreach (var det in detections)
            {
                Mat aligned = aligner.AlignFace(img, det);
                if (aligned.Empty() || !aligner.IsAlignedQualityGood(aligned))
                    continue;

                double sharpness = aligner.GetSharpness(aligned);
                float[] emb = embedder.GetEmbedding(aligned);
                float w = embedder.ComputeQualityWeight(det.Score, sharpness);

                embeddings.Add(emb);
                weights.Add(w);
                alignedFaces.Add(aligned);
            }

            var clusters = ClusterEmbeddings(embeddings, weights);

            string outDir = Path.Combine("FaceOutput", Path.GetFileNameWithoutExtension(imagePath));
            Directory.CreateDirectory(outDir);

            int personId = 1;
            foreach (var cluster in clusters)
            {
                string personDir = Path.Combine(outDir, $"Person_{personId:00}");
                Directory.CreateDirectory(personDir);

                foreach (int idx in cluster)
                {
                    string outPath = Path.Combine(personDir, $"face_{idx}.jpg");
                    Cv2.ImWrite(outPath, alignedFaces[idx]);
                }

                personId++;
            }
        }

        private PersonProfile ResolveClusterPerson(
            List<int> cluster,
            List<float[]> embeddings,
            List<float> qualityWeights,
            List<string> alignedPaths,
            Action<string>? log)
        {
            float[] clusterCentroid = ComputeClusterCentroid(cluster, embeddings);
            var representatives = SelectRepresentativeThumbnails(cluster, embeddings, qualityWeights, alignedPaths, clusterCentroid);
            var resolution = personResolver.Resolve(clusterCentroid, personRegistry);

            PersonProfile targetPerson;
            switch (resolution.Kind)
            {
                case PersonResolutionKind.Match:
                    targetPerson = resolution.Match!;
                    break;

                case PersonResolutionKind.Ambiguous:
                    targetPerson = ResolveAmbiguousPerson(resolution, representatives, log);
                    break;

                default:
                    targetPerson = CreateInteractiveNewPerson(representatives, log);
                    break;
            }

            foreach (int idx in cluster)
                personRegistry.AddEmbedding(targetPerson, embeddings[idx], qualityWeights[idx]);

            return targetPerson;
        }

        private PersonProfile ResolveAmbiguousPerson(PersonResolutionResult resolution, IReadOnlyList<string> representatives, Action<string>? log)
        {
            if (IsNonInteractiveMode())
                return CreateInteractiveNewPerson(representatives, log);

            var singleThumb = representatives.Take(1).ToList();
            return ambiguityResolver.Resolve(
                resolution,
                singleThumb,
                () => CreateInteractiveNewPerson(representatives, log),
                log);
        }

        private PersonProfile CreateInteractiveNewPerson(IReadOnlyList<string> representatives, Action<string>? log)
        {
            if (personRegistry.AutoNamingEnabled || !Options.EnableInteractiveNaming || IsNonInteractiveMode())
            {
                var autoPerson = personRegistry.CreatePerson(null);
                log?.Invoke($"Assigned automatic name: {autoPerson.Name}");
                return autoPerson;
            }

            while (true)
            {
                PersonNamingResponse response = personInteractionService.AskForNewPersonName(representatives, personRegistry.GetKnownNames());
                if (response.DontAskAgain)
                    personRegistry.AutoNamingEnabled = true;

                if (!string.IsNullOrWhiteSpace(response.Name))
                {
                    var namedPerson = personRegistry.GetOrCreatePerson(response.Name);
                    log?.Invoke($"Assigned name '{namedPerson.Name}'.");
                    return namedPerson;
                }

                if (personRegistry.AutoNamingEnabled)
                {
                    var autoPerson = personRegistry.CreatePerson(null);
                    log?.Invoke($"Assigned automatic name: {autoPerson.Name}");
                    return autoPerson;
                }

                log?.Invoke("Name is required unless 'Don't ask again' is enabled.");
            }
        }

        private static bool IsNonInteractiveMode()
        {
            try
            {
                return !Environment.UserInteractive;
            }
            catch
            {
                return true;
            }
        }

        private static float[] ComputeClusterCentroid(List<int> cluster, List<float[]> embeddings)
        {
            if (cluster.Count == 0)
                return Array.Empty<float>();

            int dim = embeddings[cluster[0]].Length;
            var centroid = new float[dim];
            foreach (int idx in cluster)
            {
                var emb = FaceComparer.NormalizeCopy(embeddings[idx]);
                for (int i = 0; i < dim; i++)
                    centroid[i] += emb[i];
            }

            float inv = 1f / cluster.Count;
            for (int i = 0; i < dim; i++)
                centroid[i] *= inv;

            return FaceComparer.NormalizeCopy(centroid);
        }

        private static List<List<int>> SortClusterMembersByCentroidDistance(IReadOnlyList<List<int>> clusters, IReadOnlyList<float[]> embeddings)
        {
            var orderedClusters = new List<List<int>>(clusters.Count);

            foreach (var cluster in clusters)
            {
                if (cluster.Count <= 1)
                {
                    orderedClusters.Add(new List<int>(cluster));
                    continue;
                }

                var centroid = ComputeClusterCentroid(cluster, embeddings.ToList());
                var orderedCluster = cluster
                    .OrderBy(idx => FaceComparer.CosineDistance(embeddings[idx], centroid))
                    .ToList();

                orderedClusters.Add(orderedCluster);
            }

            return orderedClusters;
        }

        private static IReadOnlyList<string> SelectRepresentativeThumbnails(
            List<int> cluster,
            List<float[]> embeddings,
            List<float> qualityWeights,
            List<string> alignedPaths,
            float[] clusterCentroid)
        {
            var picks = new List<int>();

            int bestQuality = cluster
                .OrderByDescending(i => qualityWeights[i])
                .FirstOrDefault();
            if (bestQuality >= 0)
                picks.Add(bestQuality);

            int centroidIdx = cluster
                .OrderBy(i => FaceComparer.CosineDistance(embeddings[i], clusterCentroid))
                .FirstOrDefault();
            if (centroidIdx >= 0 && !picks.Contains(centroidIdx))
                picks.Add(centroidIdx);

            foreach (int idx in cluster.OrderBy(_ => Random.Shared.Next()))
            {
                if (picks.Count >= 5)
                    break;

                if (!picks.Contains(idx))
                    picks.Add(idx);
            }

            return picks
                .Select(i => i >= 0 && i < alignedPaths.Count ? alignedPaths[i] : string.Empty)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Take(5)
                .ToList();
        }

        private static string SanitizeFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "UnknownPerson";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            var chars = name.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
            string sanitized = new string(chars).Trim();
            return string.IsNullOrWhiteSpace(sanitized) ? "UnknownPerson" : sanitized;
        }

        public void Dispose()
        {
            detector.Dispose();
            embedder.Dispose();
        }

        private sealed class LoadedImageItem
        {
            public LoadedImageItem(string path, Mat image)
            {
                Path = path;
                Image = image;
            }

            public string Path { get; }
            public Mat Image { get; }
        }

        private sealed class AlignedFaceItem
        {
            public AlignedFaceItem(string originalPath, string alignedPath, Mat alignedFace, float detectionScore, double sharpness)
            {
                OriginalPath = originalPath;
                AlignedPath = alignedPath;
                AlignedFace = alignedFace;
                DetectionScore = detectionScore;
                Sharpness = sharpness;
            }

            public string OriginalPath { get; }
            public string AlignedPath { get; }
            public Mat AlignedFace { get; }
            public float DetectionScore { get; }
            public double Sharpness { get; }
        }

        private sealed class EmbeddedFaceItem
        {
            public EmbeddedFaceItem(string originalPath, string alignedPath, float[] embedding, float qualityWeight)
            {
                OriginalPath = originalPath;
                AlignedPath = alignedPath;
                Embedding = embedding;
                QualityWeight = qualityWeight;
            }

            public string OriginalPath { get; }
            public string AlignedPath { get; }
            public float[] Embedding { get; }
            public float QualityWeight { get; }
        }
    }
}