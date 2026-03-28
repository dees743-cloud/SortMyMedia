using System;
using System.Collections.Generic;
using System.Linq;

namespace SortMyMedia.FaceRecognition
{
    public class FaceClusterer
    {
        private const float UncertainBandMinDistance = 0.32f;
        private const float UncertainBandMaxDistance = 0.40f;
        private const float SecondStageMergeDistanceThreshold = 0.38f;
        private const float LowVarianceThreshold = 0.020f;
        private const float MaxCrossPairwiseDistance = 0.50f;
        private const int GlobalMergeExactLimit = 600;

        private readonly float maxAssignDistance;
        private readonly float maxMergeDistance;
        private readonly int minClusterSize;
        private readonly bool enableDiagnostics;

        public event Action<int, int>? OnChunkProgress;
        public event Action<int, int>? OnGlobalMergeProgress;

        public FaceClusterer(float distanceThreshold = 0.34f, int minClusterSize = 2, bool enableDiagnostics = false)
        {
            maxMergeDistance = Math.Clamp(distanceThreshold, 0.15f, 0.60f);
            maxAssignDistance = Math.Max(0.10f, maxMergeDistance - 0.03f);
            this.minClusterSize = Math.Max(1, minClusterSize);
            this.enableDiagnostics = enableDiagnostics;

            if (enableDiagnostics)
                AppLog.Write?.Invoke($"FaceClusterer: maxAssignDistance={maxAssignDistance:F3}, maxMergeDistance={maxMergeDistance:F3}, minClusterSize={this.minClusterSize}");
        }

        public List<List<int>> Cluster(List<float[]> embeddings)
        {
            var uniformWeights = Enumerable.Repeat(1f, embeddings.Count).ToList();
            return Cluster(embeddings, uniformWeights);
        }

        public List<List<int>> Cluster(List<float[]> embeddings, List<float> qualityWeights)
        {
            var clusters = new List<List<int>>();
            var centroids = new List<float[]>();

            if (embeddings.Count == 0)
                return clusters;

            if (qualityWeights.Count != embeddings.Count)
                throw new ArgumentException("qualityWeights count must match embeddings count.");

            var normalizedEmbeddings = embeddings
                .Select(FaceComparer.NormalizeCopy)
                .ToList();

            var processingOrder = Enumerable.Range(0, normalizedEmbeddings.Count)
                .OrderByDescending(i => qualityWeights[i])
                .ToArray();

            foreach (int i in processingOrder)
            {
                float[] emb = normalizedEmbeddings[i];
                bool added = false;

                int bestClusterIndex = -1;
                float bestDistance = float.MaxValue;

                for (int c = 0; c < clusters.Count; c++)
                {
                    float distance = FaceComparer.CosineDistance(emb, centroids[c]);

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestClusterIndex = c;
                    }
                }

                if (bestClusterIndex >= 0 && bestDistance <= maxAssignDistance)
                {
                    clusters[bestClusterIndex].Add(i);
                    centroids[bestClusterIndex] = RecomputeCentroid(clusters[bestClusterIndex], normalizedEmbeddings, qualityWeights);
                    added = true;
                }

                if (!added)
                {
                    clusters.Add(new List<int> { i });
                    centroids.Add((float[])emb.Clone());
                }
            }

            MergeByCentroidSimilarity(clusters, normalizedEmbeddings, qualityWeights);
            MergeInUncertainBand(clusters, normalizedEmbeddings, qualityWeights);
            ReassignSmallClusters(clusters, normalizedEmbeddings, qualityWeights);
            EjectOutliers(clusters, normalizedEmbeddings, qualityWeights);
            SplitBimodalClusters(clusters, normalizedEmbeddings, qualityWeights);
            if (minClusterSize > 1)
                PromoteSmallClustersToOutliers(clusters);

            if (enableDiagnostics)
                PrintDistanceDiagnostics(clusters, normalizedEmbeddings);

            return clusters;
        }

        public List<List<int>> ClusterChunked(List<float[]> embeddings, List<float> qualityWeights, int chunkSize)
        {
            if (embeddings.Count == 0)
                return new List<List<int>>();

            if (qualityWeights.Count != embeddings.Count)
                throw new ArgumentException("qualityWeights count must match embeddings count.");

            int effectiveChunkSize = Math.Max(1, chunkSize);
            if (embeddings.Count <= effectiveChunkSize)
                return Cluster(embeddings, qualityWeights);

            int totalChunks = (embeddings.Count + effectiveChunkSize - 1) / effectiveChunkSize;
            AppLog.Write?.Invoke($"FaceClusterer chunking started: embeddings={embeddings.Count}, chunkSize={effectiveChunkSize}, chunks={totalChunks}");
            OnChunkProgress?.Invoke(0, totalChunks);

            var chunkClusters = BuildChunkClusters(embeddings, qualityWeights, effectiveChunkSize, totalChunks);
            if (chunkClusters.Count == 0)
                return new List<List<int>>();

            if (chunkClusters.Count == 1)
            {
                AppLog.Write?.Invoke("FaceClusterer chunking completed: only one chunk-cluster summary, skipping global merge.");
                OnGlobalMergeProgress?.Invoke(1, 1);
                return new List<List<int>> { chunkClusters[0].OriginalIndices };
            }

            AppLog.Write?.Invoke($"FaceClusterer global merge started: chunkSummaries={chunkClusters.Count}");
            OnGlobalMergeProgress?.Invoke(0, Math.Max(1, chunkClusters.Count));
            var finalClusters = MergeChunkSummariesFast(chunkClusters, embeddings, qualityWeights, effectiveChunkSize);

            AppLog.Write?.Invoke($"FaceClusterer chunked clustering completed: finalClusters={finalClusters.Count}");

            return finalClusters;
        }

        private List<List<int>> MergeChunkSummariesFast(
            List<ChunkClusterSummary> initialSummaries,
            List<float[]> embeddings,
            List<float> qualityWeights,
            int chunkSize)
        {
            var currentSummaries = initialSummaries;
            int round = 0;

            while (currentSummaries.Count > GlobalMergeExactLimit)
            {
                round++;
                int totalMergeChunks = (currentSummaries.Count + chunkSize - 1) / chunkSize;
                AppLog.Write?.Invoke($"FaceClusterer global merge round {round} started: summaries={currentSummaries.Count}, chunks={totalMergeChunks}");

                var nextSummaries = new List<ChunkClusterSummary>();
                for (int mergeChunkIndex = 0; mergeChunkIndex < totalMergeChunks; mergeChunkIndex++)
                {
                    int start = mergeChunkIndex * chunkSize;
                    int count = Math.Min(chunkSize, currentSummaries.Count - start);
                    var mergeChunk = currentSummaries.GetRange(start, count);

                    var centroids = mergeChunk.Select(c => c.Centroid).ToList();
                    var weights = mergeChunk.Select(c => c.TotalWeight).ToList();
                    EnrichCentroidsWithRepresentatives(centroids, mergeChunk);
                    var mergedGroups = Cluster(centroids, weights);

                    foreach (var mergedGroup in mergedGroups)
                    {
                        var mergedOriginalIndices = new List<int>();
                        foreach (int idx in mergedGroup)
                            mergedOriginalIndices.AddRange(mergeChunk[idx].OriginalIndices);

                        float[] centroid = RecomputeCentroid(mergedOriginalIndices, embeddings, qualityWeights);
                        float totalWeight = 0f;
                        foreach (int index in mergedOriginalIndices)
                            totalWeight += Math.Max(0.1f, qualityWeights[index]);

                        var reps = SelectRepresentativeEmbeddings(mergedOriginalIndices, embeddings, qualityWeights, centroid, 3);
                        nextSummaries.Add(new ChunkClusterSummary(mergedOriginalIndices, centroid, totalWeight, reps));
                    }

                    int progress = mergeChunkIndex + 1;
                    AppLog.Write?.Invoke($"FaceClusterer global merge round {round} progress: {progress}/{totalMergeChunks} chunk(s)");
                    OnGlobalMergeProgress?.Invoke(progress, totalMergeChunks);
                }

                if (nextSummaries.Count == currentSummaries.Count)
                {
                    AppLog.Write?.Invoke($"FaceClusterer global merge round {round} produced no reduction; stopping multi-round compression.");
                    currentSummaries = nextSummaries;
                    break;
                }

                AppLog.Write?.Invoke($"FaceClusterer global merge round {round} completed: summaries reduced {currentSummaries.Count} -> {nextSummaries.Count}");
                currentSummaries = nextSummaries;
            }

            var finalCentroids = currentSummaries.Select(c => c.Centroid).ToList();
            var finalWeights = currentSummaries.Select(c => c.TotalWeight).ToList();
            var finalMergedGroups = Cluster(finalCentroids, finalWeights);
            AppLog.Write?.Invoke($"FaceClusterer global merge final pass completed: mergedGroups={finalMergedGroups.Count}");

            var finalClusters = new List<List<int>>(finalMergedGroups.Count);
            int finalProgress = 0;
            foreach (var mergedGroup in finalMergedGroups)
            {
                var combined = new List<int>();
                foreach (int summaryIndex in mergedGroup)
                    combined.AddRange(currentSummaries[summaryIndex].OriginalIndices);

                finalClusters.Add(combined);
                finalProgress++;
                OnGlobalMergeProgress?.Invoke(finalProgress, finalMergedGroups.Count);
            }

            return finalClusters;
        }

        private List<ChunkClusterSummary> BuildChunkClusters(List<float[]> embeddings, List<float> qualityWeights, int chunkSize, int totalChunks)
        {
            var summaries = new List<ChunkClusterSummary>();
            int chunkNumber = 0;

            for (int chunkStart = 0; chunkStart < embeddings.Count; chunkStart += chunkSize)
            {
                chunkNumber++;
                int chunkCount = Math.Min(chunkSize, embeddings.Count - chunkStart);
                AppLog.Write?.Invoke($"FaceClusterer processing chunk {chunkNumber}/{totalChunks}: start={chunkStart}, count={chunkCount}");

                var chunkEmbeddings = new List<float[]>(chunkCount);
                var chunkWeights = new List<float>(chunkCount);

                for (int offset = 0; offset < chunkCount; offset++)
                {
                    int globalIndex = chunkStart + offset;
                    chunkEmbeddings.Add(embeddings[globalIndex]);
                    chunkWeights.Add(qualityWeights[globalIndex]);
                }

                var localClusters = Cluster(chunkEmbeddings, chunkWeights);
                AppLog.Write?.Invoke($"FaceClusterer chunk {chunkNumber}/{totalChunks}: localClusters={localClusters.Count}");

                int localClusterOrdinal = 0;
                foreach (var localCluster in localClusters)
                {
                    localClusterOrdinal++;
                    var originalIndices = localCluster
                        .Select(localIndex => chunkStart + localIndex)
                        .ToList();

                    float[] centroid = RecomputeCentroid(originalIndices, embeddings, qualityWeights);
                    float totalWeight = 0f;
                    foreach (int idx in originalIndices)
                        totalWeight += Math.Max(0.1f, qualityWeights[idx]);

                    var representatives = SelectRepresentativeEmbeddings(originalIndices, embeddings, qualityWeights, centroid, 3);
                    summaries.Add(new ChunkClusterSummary(originalIndices, centroid, totalWeight, representatives));
                    AppLog.Write?.Invoke($"FaceClusterer chunk {chunkNumber}/{totalChunks}: summary {localClusterOrdinal}/{localClusters.Count} created, faces={originalIndices.Count}");
                }

                OnChunkProgress?.Invoke(chunkNumber, totalChunks);
            }

            return summaries;
        }

        private void MergeByCentroidSimilarity(List<List<int>> clusters, List<float[]> embeddings, List<float> qualityWeights)
        {
            bool merged;
            do
            {
                merged = false;
                var centroids = clusters
                    .Select(c => RecomputeCentroid(c, embeddings, qualityWeights))
                    .ToList();

                for (int i = 0; i < clusters.Count && !merged; i++)
                {
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        float distance = FaceComparer.CosineDistance(centroids[i], centroids[j]);

                        float baseAllowed;
                        if (clusters[i].Count < minClusterSize || clusters[j].Count < minClusterSize)
                        {
                            baseAllowed = Math.Max(0.08f, maxMergeDistance - 0.06f);
                        }
                        else
                        {
                            float varI = ComputeInternalVariance(clusters[i], embeddings, qualityWeights, centroids[i]);
                            float varJ = ComputeInternalVariance(clusters[j], embeddings, qualityWeights, centroids[j]);
                            float maxVar = Math.Max(varI, varJ);
                            float varianceAdjustment = Math.Clamp(maxVar * 2.0f, -0.03f, 0.03f);
                            baseAllowed = maxMergeDistance - varianceAdjustment;
                        }

                        if (distance <= baseAllowed)
                        {
                            clusters[i].AddRange(clusters[j]);
                            clusters.RemoveAt(j);
                            merged = true;
                            break;
                        }
                    }
                }
            } while (merged);
        }

        private void PromoteSmallClustersToOutliers(List<List<int>> clusters)
        {
            for (int i = 0; i < clusters.Count; i++)
            {
                if (clusters[i].Count >= minClusterSize)
                    continue;

                clusters[i] = new List<int>(clusters[i]);
            }
        }

        private void MergeInUncertainBand(List<List<int>> clusters, List<float[]> normalizedEmbeddings, List<float> qualityWeights)
        {
            bool merged;
            do
            {
                merged = false;

                var weightedCentroids = clusters
                    .Select(cluster => RecomputeCentroid(cluster, normalizedEmbeddings, qualityWeights))
                    .ToList();

                for (int i = 0; i < clusters.Count && !merged; i++)
                {
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        float centroidDistance = FaceComparer.CosineDistance(weightedCentroids[i], weightedCentroids[j]);
                        if (!IsInUncertainBand(centroidDistance))
                            continue;

                        float averageWeightedDistance = ComputeAverageWeightedDistance(clusters[i], clusters[j], normalizedEmbeddings, qualityWeights);
                        if (!IsInUncertainBand(averageWeightedDistance))
                            continue;

                        if (centroidDistance >= SecondStageMergeDistanceThreshold || averageWeightedDistance >= SecondStageMergeDistanceThreshold)
                            continue;

                        float leftVariance = ComputeInternalVariance(clusters[i], normalizedEmbeddings, qualityWeights, weightedCentroids[i]);
                        float rightVariance = ComputeInternalVariance(clusters[j], normalizedEmbeddings, qualityWeights, weightedCentroids[j]);
                        if (leftVariance > LowVarianceThreshold || rightVariance > LowVarianceThreshold)
                            continue;

                        if (HasExcessiveCrossPairDistance(clusters[i], clusters[j], normalizedEmbeddings))
                            continue;

                        if (enableDiagnostics)
                        {
                            AppLog.Write?.Invoke(
                                $"FaceClusterer second-stage merge: c{i}->c{j}, centroid={centroidDistance:F3}, avgWeighted={averageWeightedDistance:F3}, varL={leftVariance:F4}, varR={rightVariance:F4}");
                        }

                        if (centroidDistance < maxMergeDistance)
                        {
                            clusters[i].AddRange(clusters[j]);
                            clusters.RemoveAt(j);
                            merged = true;
                            break;
                        }
                    }
                }
            } while (merged);
        }

        private static float ComputeAverageWeightedDistance(List<int> leftCluster, List<int> rightCluster, List<float[]> normalizedEmbeddings, List<float> qualityWeights)
        {
            if (leftCluster.Count == 0 || rightCluster.Count == 0)
                return float.MaxValue;

            float totalDistance = 0f;
            float totalPairWeight = 0f;

            foreach (int leftIndex in leftCluster)
            {
                float leftWeight = Math.Max(0.1f, qualityWeights[leftIndex]);
                foreach (int rightIndex in rightCluster)
                {
                    float rightWeight = Math.Max(0.1f, qualityWeights[rightIndex]);
                    float pairWeight = leftWeight * rightWeight;
                    totalDistance += FaceComparer.CosineDistance(normalizedEmbeddings[leftIndex], normalizedEmbeddings[rightIndex]) * pairWeight;
                    totalPairWeight += pairWeight;
                }
            }

            return totalPairWeight <= 0f ? float.MaxValue : totalDistance / totalPairWeight;
        }

        private static float ComputeInternalVariance(List<int> cluster, List<float[]> normalizedEmbeddings, List<float> qualityWeights, float[] centroid)
        {
            if (cluster.Count <= 1)
                return 0f;

            float weightedDistanceSquareSum = 0f;
            float totalWeight = 0f;

            foreach (int idx in cluster)
            {
                float weight = Math.Max(0.1f, qualityWeights[idx]);
                float distance = FaceComparer.CosineDistance(normalizedEmbeddings[idx], centroid);
                weightedDistanceSquareSum += weight * distance * distance;
                totalWeight += weight;
            }

            return totalWeight <= 0f ? float.MaxValue : weightedDistanceSquareSum / totalWeight;
        }

        private static bool IsInUncertainBand(float distance)
        {
            return distance >= UncertainBandMinDistance && distance < UncertainBandMaxDistance;
        }

        private static bool HasExcessiveCrossPairDistance(List<int> leftCluster, List<int> rightCluster, List<float[]> normalizedEmbeddings)
        {
            foreach (int leftIndex in leftCluster)
            {
                foreach (int rightIndex in rightCluster)
                {
                    if (FaceComparer.CosineDistance(normalizedEmbeddings[leftIndex], normalizedEmbeddings[rightIndex]) > MaxCrossPairwiseDistance)
                        return true;
                }
            }

            return false;
        }

        private void ReassignSmallClusters(List<List<int>> clusters, List<float[]> normalizedEmbeddings, List<float> qualityWeights)
        {
            const int smallThreshold = 3;
            float reassignThreshold = maxAssignDistance + 0.02f;

            bool changed;
            do
            {
                changed = false;
                var centroids = clusters
                    .Select(c => RecomputeCentroid(c, normalizedEmbeddings, qualityWeights))
                    .ToList();

                for (int s = clusters.Count - 1; s >= 0; s--)
                {
                    if (clusters[s].Count >= smallThreshold)
                        continue;

                    int bestTarget = -1;
                    float bestDistance = float.MaxValue;

                    for (int t = 0; t < clusters.Count; t++)
                    {
                        if (t == s || clusters[t].Count < smallThreshold)
                            continue;

                        float dist = FaceComparer.CosineDistance(centroids[s], centroids[t]);
                        if (dist < bestDistance)
                        {
                            bestDistance = dist;
                            bestTarget = t;
                        }
                    }

                    if (bestTarget < 0 || bestDistance > reassignThreshold)
                        continue;

                    bool allMatch = clusters[s].All(idx =>
                        FaceComparer.CosineDistance(normalizedEmbeddings[idx], centroids[bestTarget]) <= reassignThreshold);

                    if (!allMatch)
                        continue;

                    if (enableDiagnostics)
                        AppLog.Write?.Invoke($"FaceClusterer reassign: small cluster {s} ({clusters[s].Count} faces) → cluster {bestTarget}, dist={bestDistance:F3}");

                    clusters[bestTarget].AddRange(clusters[s]);
                    clusters.RemoveAt(s);
                    changed = true;
                    break;
                }
            } while (changed);
        }

        private static List<float[]> SelectRepresentativeEmbeddings(
            List<int> indices,
            List<float[]> embeddings,
            List<float> qualityWeights,
            float[] centroid,
            int count)
        {
            if (indices.Count == 0 || count <= 0)
                return new List<float[]>();

            var picks = new List<int>(count);

            int bestQuality = indices
                .OrderByDescending(i => qualityWeights[i])
                .First();
            picks.Add(bestQuality);

            int closest = indices
                .OrderBy(i => FaceComparer.CosineDistance(embeddings[i], centroid))
                .First();
            if (!picks.Contains(closest))
                picks.Add(closest);

            foreach (int idx in indices)
            {
                if (picks.Count >= count)
                    break;
                if (!picks.Contains(idx))
                    picks.Add(idx);
            }

            return picks
                .Where(i => i >= 0 && i < embeddings.Count)
                .Select(i => embeddings[i])
                .Take(count)
                .ToList();
        }

        private static void EnrichCentroidsWithRepresentatives(List<float[]> centroids, List<ChunkClusterSummary> summaries)
        {
            for (int i = 0; i < summaries.Count && i < centroids.Count; i++)
            {
                var reps = summaries[i].RepresentativeEmbeddings;
                if (reps.Count == 0)
                    continue;

                int dim = centroids[i].Length;
                float[] enriched = new float[dim];
                float centroidWeight = 0.6f;
                float repWeight = 0.4f / reps.Count;

                for (int d = 0; d < dim; d++)
                    enriched[d] = centroids[i][d] * centroidWeight;

                foreach (var rep in reps)
                {
                    for (int d = 0; d < dim; d++)
                        enriched[d] += rep[d] * repWeight;
                }

                float norm = 0f;
                for (int d = 0; d < dim; d++)
                    norm += enriched[d] * enriched[d];

                norm = (float)Math.Sqrt(norm);
                if (norm > 0f)
                {
                    for (int d = 0; d < dim; d++)
                        enriched[d] /= norm;
                }

                centroids[i] = enriched;
            }
        }

        private void EjectOutliers(List<List<int>> clusters, List<float[]> normalizedEmbeddings, List<float> qualityWeights)
        {
            const int minSizeForOutlierCheck = 4;
            const float sigmaMultiplier = 2.0f;

            for (int c = 0; c < clusters.Count; c++)
            {
                if (clusters[c].Count < minSizeForOutlierCheck)
                    continue;

                var centroid = RecomputeCentroid(clusters[c], normalizedEmbeddings, qualityWeights);
                var distances = clusters[c]
                    .Select(idx => (idx, dist: FaceComparer.CosineDistance(normalizedEmbeddings[idx], centroid)))
                    .ToList();

                float mean = distances.Average(d => d.dist);
                float variance = distances.Average(d => (d.dist - mean) * (d.dist - mean));
                float stddev = (float)Math.Sqrt(variance);
                float threshold = mean + sigmaMultiplier * stddev;

                var outliers = distances.Where(d => d.dist > threshold).Select(d => d.idx).ToList();
                if (outliers.Count == 0 || outliers.Count >= clusters[c].Count)
                    continue;

                foreach (int idx in outliers)
                    clusters[c].Remove(idx);

                foreach (int idx in outliers)
                    clusters.Add(new List<int> { idx });

                if (enableDiagnostics)
                    AppLog.Write?.Invoke($"FaceClusterer outlier ejection: cluster {c}, ejected {outliers.Count} face(s), mean={mean:F3}, stddev={stddev:F3}, threshold={threshold:F3}");
            }
        }

        private void SplitBimodalClusters(List<List<int>> clusters, List<float[]> normalizedEmbeddings, List<float> qualityWeights)
        {
            const int minSizeForSplit = 6;
            const float bimodalRatio = 1.5f;

            for (int c = clusters.Count - 1; c >= 0; c--)
            {
                if (clusters[c].Count < minSizeForSplit)
                    continue;

                var pairDistances = new List<float>();
                var members = clusters[c];
                for (int i = 0; i < members.Count; i++)
                {
                    for (int j = i + 1; j < members.Count; j++)
                        pairDistances.Add(FaceComparer.CosineDistance(normalizedEmbeddings[members[i]], normalizedEmbeddings[members[j]]));
                }

                if (pairDistances.Count < 3)
                    continue;

                pairDistances.Sort();
                float median = pairDistances[pairDistances.Count / 2];
                if (median <= 0.001f)
                    continue;

                int topStart = (int)(pairDistances.Count * 0.80);
                float topMean = pairDistances.Skip(topStart).Average();

                if (topMean <= median * bimodalRatio)
                    continue;

                var centroid = RecomputeCentroid(members, normalizedEmbeddings, qualityWeights);
                var sorted = members
                    .OrderBy(idx => FaceComparer.CosineDistance(normalizedEmbeddings[idx], centroid))
                    .ToList();

                int splitPoint = sorted.Count / 2;
                var closeHalf = sorted.Take(splitPoint).ToList();
                var farHalf = sorted.Skip(splitPoint).ToList();

                if (closeHalf.Count < minClusterSize || farHalf.Count < minClusterSize)
                    continue;

                clusters[c] = closeHalf;
                clusters.Add(farHalf);

                if (enableDiagnostics)
                    AppLog.Write?.Invoke($"FaceClusterer bimodal split: cluster {c}, median={median:F3}, topMean={topMean:F3}, close={closeHalf.Count}, far={farHalf.Count}");
            }
        }

        private void PrintDistanceDiagnostics(List<List<int>> clusters, List<float[]> normalizedEmbeddings)
        {
            if (clusters.Count == 0)
                return;

            float intraSum = 0f;
            int intraCount = 0;

            foreach (var cluster in clusters)
            {
                for (int i = 0; i < cluster.Count; i++)
                {
                    for (int j = i + 1; j < cluster.Count; j++)
                    {
                        intraSum += FaceComparer.CosineDistance(normalizedEmbeddings[cluster[i]], normalizedEmbeddings[cluster[j]]);
                        intraCount++;
                    }
                }
            }

            float interSum = 0f;
            int interCount = 0;
            var centroids = clusters.Select(c => RecomputeCentroid(c, normalizedEmbeddings, Enumerable.Repeat(1f, normalizedEmbeddings.Count).ToList())).ToList();
            for (int i = 0; i < centroids.Count; i++)
            {
                for (int j = i + 1; j < centroids.Count; j++)
                {
                    interSum += FaceComparer.CosineDistance(centroids[i], centroids[j]);
                    interCount++;
                }
            }

            float avgIntra = intraCount > 0 ? intraSum / intraCount : 0f;
            float avgInter = interCount > 0 ? interSum / interCount : 0f;
            AppLog.Write?.Invoke($"FaceClusterer diagnostics: avgIntraDistance={avgIntra:F3}, avgInterDistance={avgInter:F3}");
        }

        private static float[] RecomputeCentroid(List<int> cluster, List<float[]> embeddings, List<float> qualityWeights)
        {
            const int trimMinSize = 6;

            IEnumerable<int> members = cluster;

            if (cluster.Count >= trimMinSize)
            {
                float[] roughCentroid = ComputeRawCentroid(cluster, embeddings, qualityWeights);
                int farthest = cluster
                    .OrderByDescending(idx => FaceComparer.CosineDistance(embeddings[idx], roughCentroid))
                    .First();
                members = cluster.Where(idx => idx != farthest);
            }

            return ComputeRawCentroid(members, embeddings, qualityWeights);
        }

        private static float[] ComputeRawCentroid(IEnumerable<int> indices, List<float[]> embeddings, List<float> qualityWeights)
        {
            int dim = -1;
            float[] centroid = null!;
            float totalWeight = 0f;

            foreach (int idx in indices)
            {
                float[] emb = embeddings[idx];
                if (dim < 0)
                {
                    dim = emb.Length;
                    centroid = new float[dim];
                }

                float w = Math.Max(0.1f, qualityWeights[idx]);
                totalWeight += w;
                for (int d = 0; d < dim; d++)
                    centroid[d] += emb[d] * w;
            }

            if (dim < 0)
                return Array.Empty<float>();

            float inv = totalWeight > 0f ? 1f / totalWeight : 1f;
            for (int d = 0; d < dim; d++)
                centroid[d] *= inv;

            float norm = 0f;
            for (int d = 0; d < dim; d++)
                norm += centroid[d] * centroid[d];

            norm = (float)Math.Sqrt(norm);
            if (norm > 0f)
            {
                for (int d = 0; d < dim; d++)
                    centroid[d] /= norm;
            }

            return centroid;
        }

        private sealed class ChunkClusterSummary
        {
            public ChunkClusterSummary(List<int> originalIndices, float[] centroid, float totalWeight, List<float[]>? representativeEmbeddings = null)
            {
                OriginalIndices = originalIndices;
                Centroid = centroid;
                TotalWeight = totalWeight;
                RepresentativeEmbeddings = representativeEmbeddings ?? new List<float[]>();
            }

            public List<int> OriginalIndices { get; }
            public float[] Centroid { get; }
            public float TotalWeight { get; }
            public List<float[]> RepresentativeEmbeddings { get; }
        }
    }
}