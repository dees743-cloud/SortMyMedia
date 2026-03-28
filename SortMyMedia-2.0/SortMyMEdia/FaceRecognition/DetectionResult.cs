namespace SortMyMedia.FaceRecognition
{
    public class DetectionResult
    {
        public OpenCvSharp.Rect BBox { get; set; }
        public OpenCvSharp.Point2f[] Keypoints { get; set; } = Array.Empty<OpenCvSharp.Point2f>();
        public float Score { get; set; }
    }
}