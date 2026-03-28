using System;
using System.Collections.Generic;

namespace SortMyMedia.FaceRecognition
{
    public class FaceClusteringService
    {
        private readonly FaceClusterer clusterer;
        private readonly int chunkSize;

        public event Action<int, int>? OnChunkProgress;
        public event Action<int, int>? OnGlobalMergeProgress;

        public FaceClusteringService(float distanceThreshold = 0.34f, int minClusterSize = 2, bool enableDiagnostics = false, int chunkSize = 1200)
        {
            clusterer = new FaceClusterer(distanceThreshold, minClusterSize, enableDiagnostics);
            this.chunkSize = chunkSize < 1 ? 1 : chunkSize;

            clusterer.OnChunkProgress += (completed, total) => OnChunkProgress?.Invoke(completed, total);
            clusterer.OnGlobalMergeProgress += (completed, total) => OnGlobalMergeProgress?.Invoke(completed, total);
        }

        public List<List<int>> Cluster(List<float[]> embeddings, List<float> weights)
        {
            AppLog.Write?.Invoke($"FaceClusteringService started: embeddings={embeddings.Count}, chunkSize={chunkSize}");
            var result = clusterer.ClusterChunked(embeddings, weights, chunkSize);
            AppLog.Write?.Invoke($"FaceClusteringService completed: finalClusters={result.Count}");
            return result;
        }
    }
}