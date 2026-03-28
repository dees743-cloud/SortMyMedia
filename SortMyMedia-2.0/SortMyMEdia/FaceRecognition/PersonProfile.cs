using System;
using System.Collections.Generic;

namespace SortMyMedia.FaceRecognition
{
    public sealed class PersonProfile
    {
        private readonly List<float[]> embeddings = new();

        public PersonProfile(int id, string name)
        {
            Id = id;
            Name = name;
            Centroid = Array.Empty<float>();
        }

        public int Id { get; }
        public string Name { get; set; }
        public IReadOnlyList<float[]> Embeddings => embeddings;
        public float[] Centroid { get; private set; }
        public float AverageQuality { get; private set; }

        public void AddEmbedding(float[] embedding, float quality)
        {
            if (embedding == null || embedding.Length == 0)
                return;

            embeddings.Add(FaceComparer.NormalizeCopy(embedding));
            RecomputeCentroid();

            float count = embeddings.Count;
            AverageQuality = ((AverageQuality * (count - 1f)) + quality) / count;
        }

        private void RecomputeCentroid()
        {
            if (embeddings.Count == 0)
            {
                Centroid = Array.Empty<float>();
                return;
            }

            int dim = embeddings[0].Length;
            var centroid = new float[dim];

            foreach (var emb in embeddings)
            {
                for (int i = 0; i < dim; i++)
                    centroid[i] += emb[i];
            }

            float inv = 1f / embeddings.Count;
            for (int i = 0; i < dim; i++)
                centroid[i] *= inv;

            Centroid = FaceComparer.NormalizeCopy(centroid);
        }
    }
}
