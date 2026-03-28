using System;

namespace SortMyMedia.FaceRecognition
{
    public static class FaceComparer
    {
        // Cosine similarity tussen twee embeddings
        public static float Cosine(float[] a, float[] b)
        {
            if (a == null || b == null) return 0f;
            if (a.Length != b.Length) return 0f;

            float dot = 0f;

            for (int i = 0; i < a.Length; i++)
                dot += a[i] * b[i];

            return dot; // embeddings zijn al L2-genormaliseerd
        }

        public static float CosineDistance(float[] a, float[] b)
        {
            return 1f - Cosine(a, b);
        }

        public static float[] NormalizeCopy(float[] emb)
        {
            if (emb == null || emb.Length == 0)
                return Array.Empty<float>();

            float sum = 0f;
            for (int i = 0; i < emb.Length; i++)
                sum += emb[i] * emb[i];

            float norm = MathF.Sqrt(sum);
            if (norm <= 1e-6f)
                return (float[])emb.Clone();

            float inv = 1f / norm;
            float[] normalized = new float[emb.Length];
            for (int i = 0; i < emb.Length; i++)
                normalized[i] = emb[i] * inv;

            return normalized;
        }

        // Bepaal of twee embeddings dezelfde persoon zijn
        public static bool IsSamePerson(float[] a, float[] b, float threshold = 0.45f)
        {
            return Cosine(a, b) >= threshold;
        }
    }
}