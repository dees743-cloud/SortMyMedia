using OpenCvSharp;

namespace SortMyMedia.FaceRecognition
{
    public static class FaceAligner
    {
        // Standaard 5-landmark template (InsightFace)
        private static readonly Point2f[] Template =
        {
    new Point2f(30.2946f, 51.6963f),
    new Point2f(65.5318f, 51.5014f),
    new Point2f(48.0252f, 71.7366f),
    new Point2f(33.5493f, 92.3655f),
    new Point2f(62.7299f, 92.2041f)
};

        public static Mat Align(Mat face, Point2f[] keypoints)
        {
            if (keypoints == null || keypoints.Length != 5)
                return face;

            var src = keypoints;
            var dst = Template;

            // Point2f[] omzetten naar Mat voor EstimateAffinePartial2D
            using var srcMat = new Mat(5, 1, MatType.CV_32FC2);
            using var dstMat = new Mat(5, 1, MatType.CV_32FC2);

            for (int i = 0; i < 5; i++)
            {
                srcMat.Set(i, 0, src[i]);
                dstMat.Set(i, 0, dst[i]);
            }

            Mat transform = Cv2.EstimateAffinePartial2D(srcMat, dstMat);

            Mat aligned = new Mat();
            Cv2.WarpAffine(face, aligned, transform, new OpenCvSharp.Size(112, 112));

            return aligned;
        }
    }
}