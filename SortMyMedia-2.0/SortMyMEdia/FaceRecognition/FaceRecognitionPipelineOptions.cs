using System;

namespace SortMyMedia.FaceRecognition
{
    public sealed class FaceRecognitionPipelineOptions
    {
        private static readonly int[] AllowedScrfdSizes = { 640, 800, 960, 1280 };

        public bool EnableBatching { get; set; } = true;
        public int EmbeddingBatchSize { get; set; } = 16;
        public int ScrfdInputSize { get; set; } = 640;
        public bool EnableMultiCropEmbedding { get; set; } = true;
        public int MultiCropShiftPixels { get; set; } = 4;
        public float MultiCropScale { get; set; } = 0.92f;
        public float ClusterDistanceThreshold { get; set; } = 0.40f;
        public int MinClusterSize { get; set; } = 2;
        public double MinAlignedSharpness { get; set; } = 100.0;
        public bool EnablePoseQualityCheck { get; set; } = true;
        public float MaxEyeTiltRatio { get; set; } = 0.12f;
        public float MinEyeDistanceRatio { get; set; } = 0.22f;
        public float MinDetectionScore { get; set; } = 0.50f;
        public bool EnableDetailedLogging { get; set; }
        public bool EnableInteractiveNaming { get; set; } = true;
        public float ResolverStrictDistanceThreshold { get; set; } = 0.30f;
        public float ResolverLooseDistanceThreshold { get; set; } = 0.50f;
        public int ResolverMaxAmbiguousCandidates { get; set; } = 5;
        public int CpuPreprocessWorkers { get; set; } = Math.Max(1, Environment.ProcessorCount / 2);
        public int LoadedImageQueueCapacity { get; set; } = 8;
        public int AlignedFaceQueueCapacity { get; set; } = 64;

        public FaceRecognitionPipelineOptions CloneValidated()
        {
            var clone = (FaceRecognitionPipelineOptions)MemberwiseClone();

            clone.EmbeddingBatchSize = Math.Max(1, clone.EmbeddingBatchSize);
            clone.CpuPreprocessWorkers = Math.Max(1, clone.CpuPreprocessWorkers);
            clone.LoadedImageQueueCapacity = Math.Max(1, clone.LoadedImageQueueCapacity);
            clone.AlignedFaceQueueCapacity = Math.Max(1, clone.AlignedFaceQueueCapacity);
            clone.MultiCropShiftPixels = Math.Clamp(clone.MultiCropShiftPixels, 0, 12);
            clone.MultiCropScale = Math.Clamp(clone.MultiCropScale, 0.80f, 1.00f);
            clone.ClusterDistanceThreshold = Math.Clamp(clone.ClusterDistanceThreshold, 0.15f, 0.60f);
            clone.MinClusterSize = Math.Max(1, clone.MinClusterSize);
            clone.MinAlignedSharpness = Math.Clamp(clone.MinAlignedSharpness, 20.0, 500.0);
            clone.MaxEyeTiltRatio = Math.Clamp(clone.MaxEyeTiltRatio, 0.02f, 0.30f);
            clone.MinEyeDistanceRatio = Math.Clamp(clone.MinEyeDistanceRatio, 0.10f, 0.40f);
            clone.MinDetectionScore = Math.Clamp(clone.MinDetectionScore, 0.10f, 0.95f);
            clone.ResolverStrictDistanceThreshold = Math.Clamp(clone.ResolverStrictDistanceThreshold, 0.10f, 0.50f);
            clone.ResolverLooseDistanceThreshold = Math.Clamp(clone.ResolverLooseDistanceThreshold, clone.ResolverStrictDistanceThreshold + 0.01f, 0.70f);
            clone.ResolverMaxAmbiguousCandidates = Math.Clamp(clone.ResolverMaxAmbiguousCandidates, 1, 5);

            if (Array.IndexOf(AllowedScrfdSizes, clone.ScrfdInputSize) < 0)
                clone.ScrfdInputSize = 640;

            return clone;
        }
    }
}
