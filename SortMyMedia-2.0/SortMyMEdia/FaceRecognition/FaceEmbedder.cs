using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SortMyMedia.FaceRecognition
{
    public class FaceEmbedder : IDisposable
    {
        private const int ModelInputSize = 112;

        private readonly InferenceSession session;
        private readonly string inputName;
        private readonly bool useNhwcInput;
        private readonly bool usingCuda;
        private readonly bool enableDetailedLogging;
        private readonly bool enableMultiCrop;
        private readonly float multiCropScale;
        private readonly (int dx, int dy)[] cropOffsets;
        private readonly string modelName;
        private readonly object inferenceLock = new();

        public FaceEmbedder(FaceRecognitionPipelineOptions? options = null)
        {
            var resolvedOptions = (options ?? new FaceRecognitionPipelineOptions()).CloneValidated();
            enableDetailedLogging = resolvedOptions.EnableDetailedLogging;
            enableMultiCrop = resolvedOptions.EnableMultiCropEmbedding;
            multiCropScale = resolvedOptions.MultiCropScale;

            int shift = resolvedOptions.MultiCropShiftPixels;
            cropOffsets = enableMultiCrop
                ? new[] { (0, 0), (-shift, 0), (shift, 0), (0, -shift), (0, shift) }
                : new[] { (0, 0) };

            session = CreatePreferredEmbeddingSession(out modelName, out usingCuda);
            inputName = session.InputMetadata.Keys.First();

            var inputMeta = session.InputMetadata[inputName];
            int[] dims = inputMeta.Dimensions.ToArray();
            useNhwcInput = dims.Length == 4 && dims[3] == 3;

            Console.WriteLine($"{modelName}: input layout {(useNhwcInput ? "NHWC" : "NCHW")}; provider={(usingCuda ? "CUDA" : "CPU")}");
            if (enableDetailedLogging)
                Console.WriteLine($"{modelName}: multi-crop {(enableMultiCrop ? "enabled" : "disabled")}, crops={cropOffsets.Length}");
        }

        public float[] GetEmbedding(Mat alignedFace)
        {
            if (alignedFace == null || alignedFace.Empty())
                return Array.Empty<float>();

            var list = GetEmbeddingsBatch(new[] { alignedFace });
            return list.Count == 0 ? Array.Empty<float>() : list[0];
        }

        public IReadOnlyList<float[]> GetEmbeddingsBatch(IReadOnlyList<Mat> alignedFaces, bool enableDetailedLogging = false)
        {
            if (alignedFaces == null || alignedFaces.Count == 0)
                return Array.Empty<float[]>();

            try
            {
                int batchSize = alignedFaces.Count;
                int cropCount = cropOffsets.Length;
                int effectiveBatch = batchSize * cropCount;
                int inputLength = effectiveBatch * 3 * ModelInputSize * ModelInputSize;

                float[] inputBuffer = ArrayPool<float>.Shared.Rent(inputLength);
                try
                {
                    FillInputBuffer(alignedFaces, inputBuffer);

                    DenseTensor<float> tensor = useNhwcInput
                        ? new DenseTensor<float>(new Memory<float>(inputBuffer, 0, inputLength), new[] { effectiveBatch, ModelInputSize, ModelInputSize, 3 })
                        : new DenseTensor<float>(new Memory<float>(inputBuffer, 0, inputLength), new[] { effectiveBatch, 3, ModelInputSize, ModelInputSize });

                    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(inputName, tensor) };
                    var gpuTimer = Stopwatch.StartNew();
                    IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;

                    lock (inferenceLock)
                    {
                        results = session.Run(inputs);
                    }

                    gpuTimer.Stop();
                    if (enableDetailedLogging || this.enableDetailedLogging)
                        Console.WriteLine($"{modelName} batch={batchSize}, crops={cropCount}, inference={gpuTimer.Elapsed.TotalMilliseconds:F1}ms provider={(usingCuda ? "CUDA" : "CPU")}");

                    using (results)
                    {
                        float[] output = SelectEmbeddingOutput(results).ToArray();
                        int embeddingSize = output.Length / effectiveBatch;
                        if (embeddingSize <= 0)
                            return Array.Empty<float[]>();

                        var embeddings = new float[batchSize][];
                        for (int i = 0; i < batchSize; i++)
                        {
                            var embedding = new float[embeddingSize];
                            float totalCropWeight = 0f;
                            for (int crop = 0; crop < cropCount; crop++)
                            {
                                float cropWeight = (crop == 0 && cropCount > 1) ? 0.40f : (cropCount > 1 ? (0.60f / (cropCount - 1)) : 1.0f);
                                totalCropWeight += cropWeight;
                                int sourceOffset = ((i * cropCount) + crop) * embeddingSize;
                                for (int d = 0; d < embeddingSize; d++)
                                    embedding[d] += output[sourceOffset + d] * cropWeight;
                            }

                            float invTotalCropWeight = 1f / totalCropWeight;
                            for (int d = 0; d < embeddingSize; d++)
                                embedding[d] *= invTotalCropWeight;

                            L2NormalizeInPlace(embedding);
                            embeddings[i] = embedding;
                        }

                        return embeddings;
                    }
                }
                finally
                {
                    ArrayPool<float>.Shared.Return(inputBuffer, clearArray: false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{modelName} inference failed ({(usingCuda ? "CUDA" : "CPU")}): {ex.Message}");
                return Array.Empty<float[]>();
            }
        }

        private void FillInputBuffer(IReadOnlyList<Mat> alignedFaces, float[] inputBuffer)
        {
            int outputIndex = 0;
            foreach (Mat alignedFace in alignedFaces)
            {
                using Mat input = EnsureBgrInput(alignedFace);
                using Mat resized = new Mat();
                Cv2.Resize(input, resized, new OpenCvSharp.Size(ModelInputSize, ModelInputSize));

                foreach (var (dx, dy) in cropOffsets)
                {
                    using Mat crop = CreateShiftedCrop(resized, dx, dy);
                    WriteCropToBuffer(crop, inputBuffer, outputIndex);
                    outputIndex++;
                }
            }
        }

        private Mat CreateShiftedCrop(Mat source, int dx, int dy)
        {
            if (!enableMultiCrop || (dx == 0 && dy == 0))
                return source.Clone();

            int cropSize = Math.Clamp((int)(ModelInputSize * multiCropScale), 96, ModelInputSize);
            int x = Math.Clamp(((ModelInputSize - cropSize) / 2) + dx, 0, ModelInputSize - cropSize);
            int y = Math.Clamp(((ModelInputSize - cropSize) / 2) + dy, 0, ModelInputSize - cropSize);

            using Mat roi = new Mat(source, new Rect(x, y, cropSize, cropSize));
            Mat output = new Mat();
            Cv2.Resize(roi, output, new OpenCvSharp.Size(ModelInputSize, ModelInputSize));
            return output;
        }

        private void WriteCropToBuffer(Mat crop, float[] buffer, int outputIndex)
        {
            int planeSize = ModelInputSize * ModelInputSize;

            if (useNhwcInput)
            {
                int offset = outputIndex * planeSize * 3;
                for (int y = 0; y < ModelInputSize; y++)
                {
                    for (int x = 0; x < ModelInputSize; x++)
                    {
                        Vec3b pixel = crop.At<Vec3b>(y, x);
                        int index = offset + ((y * ModelInputSize + x) * 3);
                        buffer[index] = (pixel.Item2 - 127.5f) / 128f;
                        buffer[index + 1] = (pixel.Item1 - 127.5f) / 128f;
                        buffer[index + 2] = (pixel.Item0 - 127.5f) / 128f;
                    }
                }

                return;
            }

            int batchOffset = outputIndex * 3 * planeSize;
            for (int y = 0; y < ModelInputSize; y++)
            {
                for (int x = 0; x < ModelInputSize; x++)
                {
                    Vec3b pixel = crop.At<Vec3b>(y, x);
                    int pixelIndex = y * ModelInputSize + x;
                    buffer[batchOffset + pixelIndex] = (pixel.Item2 - 127.5f) / 128f;
                    buffer[batchOffset + planeSize + pixelIndex] = (pixel.Item1 - 127.5f) / 128f;
                    buffer[batchOffset + (2 * planeSize) + pixelIndex] = (pixel.Item0 - 127.5f) / 128f;
                }
            }
        }

        private static Tensor<float> SelectEmbeddingOutput(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            Tensor<float>? best = null;
            int bestDim = -1;

            foreach (var result in results)
            {
                var tensor = result.AsTensor<float>();
                int[] dims = tensor.Dimensions.ToArray();
                int lastDim = dims.Length > 0 ? dims[^1] : (int)tensor.Length;
                if (lastDim > bestDim)
                {
                    bestDim = lastDim;
                    best = tensor;
                }
            }

            if (best == null)
                throw new InvalidOperationException("No float embedding output found in ONNX inference results.");

            return best;
        }

        private static InferenceSession CreatePreferredEmbeddingSession(out string modelName, out bool usingCuda)
        {
            var candidates = new[]
            {
                (Path: "adaface_ir101_webface12m.onnx", Name: "AdaFace"),
                (Path: "adaface.onnx", Name: "AdaFace"),
                (Path: "magface_r100.onnx", Name: "MagFace"),
                (Path: "arcface_r100.onnx", Name: "ArcFace")
            };

            var allSearchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var loadErrors = new List<string>();

            foreach (var candidate in candidates)
            {
                try
                {
                    string modelPath = ResolveModelPath(candidate.Path, allSearchedPaths);
                    modelName = candidate.Name;
                    return OnnxRuntimeSessionFactory.Create(modelPath, candidate.Name, out usingCuda);
                }
                catch (Exception ex)
                {
                    loadErrors.Add($"{candidate.Name}: {ex.Message}");
                }
            }

            string searched = allSearchedPaths.Count == 0
                ? "(no model paths were probed)"
                : string.Join(Environment.NewLine, allSearchedPaths.Take(20));
            string details = loadErrors.Count == 0
                ? "No additional loader errors were captured."
                : string.Join(" | ", loadErrors);

            throw new InvalidOperationException(
                $"Failed to load any embedding model (AdaFace/MagFace/ArcFace). Ensure one of the model files is present in the app folder or model directory. Searched paths:{Environment.NewLine}{searched}. Details: {details}");
        }

        private static string ResolveModelPath(string fileName, ISet<string> searchedPaths)
        {
            foreach (string candidatePath in EnumerateModelPathCandidates(fileName))
            {
                if (string.IsNullOrWhiteSpace(candidatePath))
                    continue;

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(candidatePath);
                }
                catch
                {
                    continue;
                }

                searchedPaths.Add(fullPath);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            throw new FileNotFoundException($"Model file '{fileName}' not found.");
        }

        private static IEnumerable<string> EnumerateModelPathCandidates(string fileName)
        {
            if (Path.IsPathRooted(fileName))
                yield return fileName;

            yield return fileName;

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string currentDir = Environment.CurrentDirectory;

            yield return Path.Combine(currentDir, fileName);
            yield return Path.Combine(baseDir, fileName);
            yield return Path.Combine(baseDir, "models", fileName);
            yield return Path.Combine(baseDir, "FaceRecognition", "models", fileName);

            string probeRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
            yield return Path.Combine(probeRoot, fileName);
            yield return Path.Combine(probeRoot, "models", fileName);

            string tfmFolder = new DirectoryInfo(Path.GetFullPath(baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))).Name;
            yield return Path.Combine(probeRoot, "bin", "Debug", tfmFolder, fileName);
            yield return Path.Combine(probeRoot, "bin", "Release", tfmFolder, fileName);

            string binRoot = Path.Combine(probeRoot, "bin");
            if (!Directory.Exists(binRoot))
                yield break;

            foreach (string found in Directory.EnumerateFiles(binRoot, fileName, SearchOption.AllDirectories))
                yield return found;
        }

        private static void L2NormalizeInPlace(float[] embedding)
        {
            float sum = 0f;
            for (int i = 0; i < embedding.Length; i++)
                sum += embedding[i] * embedding[i];

            float norm = MathF.Sqrt(sum);
            if (norm <= 0f)
                return;

            float invNorm = 1f / norm;
            for (int i = 0; i < embedding.Length; i++)
                embedding[i] *= invNorm;
        }

        private static Mat EnsureBgrInput(Mat src)
        {
            if (src.Dims > 2)
            {
                int h = (int)src.Size(0);
                int c = (int)src.Size(src.Dims - 1);

                if ((c == 1 || c == 3 || c == 4) && h > 0)
                {
                    using Mat reshaped = src.Reshape(c, h);
                    return EnsureBgrInput(reshaped);
                }

                throw new InvalidOperationException($"Unsupported input dims for embedder: dims={src.Dims}");
            }

            int channels = src.Channels();
            if (channels == 3)
                return src.Clone();

            Mat dst = new Mat();
            if (channels == 1)
            {
                Cv2.CvtColor(src, dst, ColorConversionCodes.GRAY2BGR);
                return dst;
            }

            if (channels == 4)
            {
                Cv2.CvtColor(src, dst, ColorConversionCodes.BGRA2BGR);
                return dst;
            }

            throw new InvalidOperationException($"Unsupported input channels for embedder: {channels}");
        }

        public void Dispose()
        {
            session.Dispose();
        }
    }
}