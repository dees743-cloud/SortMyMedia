using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace SortMyMedia.FaceRecognition
{
    public class FaceEmbeddingService : IDisposable
    {
        private readonly FaceEmbedder embedder;

        public FaceEmbeddingService(FaceRecognitionPipelineOptions? options = null)
        {
            embedder = new FaceEmbedder(options);
        }

        public float[] GetEmbedding(Mat aligned)
        {
            var emb = embedder.GetEmbedding(aligned);
            return Normalize(emb);
        }

        public IReadOnlyList<float[]> GetEmbeddings(IReadOnlyList<Mat> alignedFaces, bool enableBatching = true, int batchSize = 16, bool enableDetailedLogging = false)
        {
            if (alignedFaces == null || alignedFaces.Count == 0)
                return Array.Empty<float[]>();

            if (!enableBatching || alignedFaces.Count == 1 || batchSize <= 1)
            {
                var singleResults = new float[alignedFaces.Count][];
                for (int i = 0; i < alignedFaces.Count; i++)
                    singleResults[i] = GetEmbedding(alignedFaces[i]);

                return singleResults;
            }

            var results = new List<float[]>(alignedFaces.Count);
            for (int start = 0; start < alignedFaces.Count; start += batchSize)
            {
                int currentSize = Math.Min(batchSize, alignedFaces.Count - start);
                var batch = new Mat[currentSize];
                for (int i = 0; i < currentSize; i++)
                    batch[i] = alignedFaces[start + i];

                var batchEmbeddings = embedder.GetEmbeddingsBatch(batch, enableDetailedLogging);
                for (int i = 0; i < batchEmbeddings.Count; i++)
                    results.Add(batchEmbeddings[i]);
            }

            return results;
        }

        public float ComputeQualityWeight(float detectionScore, double sharpness)
        {
            float scoreFactor = Math.Clamp(detectionScore, 0.10f, 1.0f);
            float sharpnessFactor = (float)Math.Clamp(sharpness / 250.0, 0.25, 1.5);
            return scoreFactor * sharpnessFactor;
        }

        private float[] Normalize(float[] emb)
        {
            float sum = 0f;
            foreach (var v in emb)
                sum += v * v;

            float norm = MathF.Sqrt(sum);
            if (norm < 1e-6f)
                return emb;

            float[] outEmb = new float[emb.Length];
            for (int i = 0; i < emb.Length; i++)
                outEmb[i] = emb[i] / norm;

            return outEmb;
        }

        public void Dispose()
        {
            embedder.Dispose();
        }
    }
}