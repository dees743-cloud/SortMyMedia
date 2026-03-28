using OpenCvSharp;
using System;
using System.Linq;

namespace SortMyMedia.FaceRecognition
{
    public class FaceAlignmentService
    {
        private readonly double minSharpness;
        private readonly bool enablePoseQualityCheck;
        private readonly float maxEyeTiltRatio;
        private readonly float minEyeDistanceRatio;

        public FaceAlignmentService(
            double minSharpnessThreshold = 80.0,
            bool enablePoseQualityCheck = true,
            float maxEyeTiltRatio = 0.12f,
            float minEyeDistanceRatio = 0.22f)
        {
            minSharpness = minSharpnessThreshold;
            this.enablePoseQualityCheck = enablePoseQualityCheck;
            this.maxEyeTiltRatio = maxEyeTiltRatio;
            this.minEyeDistanceRatio = minEyeDistanceRatio;
        }

        public Mat AlignFace(Mat img, DetectionResult det, float paddingFactor = 0.15f)
        {
            Rect padded = ApplyPadding(det.BBox, paddingFactor, img.Width, img.Height);

            if (!IsValidROI(padded, img))
                return new Mat();

            Mat cropped = new Mat(img, padded);

            var kp = det.Keypoints
                .Select(p => new Point2f(p.X - padded.X, p.Y - padded.Y))
                .ToArray();

            Mat aligned = FaceAligner.Align(cropped, kp);
            return aligned;
        }

        public bool IsAlignedQualityGood(Mat aligned)
        {
            if (aligned.Empty())
                return false;

            return GetSharpness(aligned) >= minSharpness;
        }

        public bool IsPoseQualityGood(DetectionResult det)
        {
            if (!enablePoseQualityCheck)
                return true;

            if (det.Keypoints == null || det.Keypoints.Length < 2)
                return false;

            var leftEye = det.Keypoints[0];
            var rightEye = det.Keypoints[1];
            float eyeDx = rightEye.X - leftEye.X;
            if (eyeDx <= 1f)
                return false;

            float eyeDy = Math.Abs(rightEye.Y - leftEye.Y);
            float eyeTilt = eyeDy / eyeDx;
            if (eyeTilt > maxEyeTiltRatio)
                return false;

            float eyeDistanceRatio = eyeDx / Math.Max(1f, det.BBox.Width);
            if (eyeDistanceRatio < minEyeDistanceRatio)
                return false;

            return true;
        }

        public double GetSharpness(Mat aligned)
        {
            using Mat gray = new();
            Cv2.CvtColor(aligned, gray, ColorConversionCodes.BGR2GRAY);

            using Mat lap = new();
            Cv2.Laplacian(gray, lap, MatType.CV_64F);

            Cv2.MeanStdDev(lap, out _, out Scalar stddev);
            return stddev.Val0 * stddev.Val0;
        }

        public bool IsLikelyFaceByColor(Mat aligned)
        {
            if (aligned.Empty())
                return false;

            int totalPixels = aligned.Rows * aligned.Cols;
            if (totalPixels <= 0)
                return false;

            int achromaticCount = 0;
            int skinLikeCount = 0;

            for (int y = 0; y < aligned.Rows; y += 2)
            {
                for (int x = 0; x < aligned.Cols; x += 2)
                {
                    Vec3b pixel = aligned.At<Vec3b>(y, x);
                    int b = pixel.Item0;
                    int g = pixel.Item1;
                    int r = pixel.Item2;

                    if (Math.Abs(r - g) < 15 && Math.Abs(g - b) < 15 && Math.Abs(r - b) < 15)
                        achromaticCount++;

                    if (r > 95 && g > 40 && b > 20 && (r - g) > 15 && r > g && r > b)
                        skinLikeCount++;
                }
            }

            int sampled = ((aligned.Rows + 1) / 2) * ((aligned.Cols + 1) / 2);
            float achromaticRatio = achromaticCount / (float)sampled;
            float skinRatio = skinLikeCount / (float)sampled;

            return !(achromaticRatio > 0.70f && skinRatio < 0.05f);
        }

        public bool IsSkinTonePresent(Mat aligned, float minSkinRatio = 0.25f)
        {
            if (aligned.Empty())
                return false;

            using Mat hsv = new();
            Cv2.CvtColor(aligned, hsv, ColorConversionCodes.BGR2HSV);

            int totalPixels = aligned.Rows * aligned.Cols;
            if (totalPixels <= 0)
                return false;

            int skinPixels = 0;
            for (int y = 0; y < aligned.Rows; y++)
            {
                for (int x = 0; x < aligned.Cols; x++)
                {
                    Vec3b pixel = hsv.At<Vec3b>(y, x);
                    int h = pixel.Item0;
                    int s = pixel.Item1;
                    int v = pixel.Item2;

                    if (h <= 25 && s >= 20 && s <= 200 && v >= 40)
                        skinPixels++;
                }
            }

            return skinPixels / (float)totalPixels >= minSkinRatio;
        }

        private bool IsValidROI(Rect r, Mat img)
        {
            return r.X >= 0 && r.Y >= 0 &&
                   r.X + r.Width <= img.Width &&
                   r.Y + r.Height <= img.Height;
        }

        private static Rect ApplyPadding(Rect box, float factor, int imgW, int imgH)
        {
            int padX = (int)(box.Width * factor);
            int padY = (int)(box.Height * factor);

            int x = Math.Max(0, box.X - padX);
            int y = Math.Max(0, box.Y - padY);
            int w = Math.Min(imgW - x, box.Width + padX * 2);
            int h = Math.Min(imgH - y, box.Height + padY * 2);

            return new Rect(x, y, w, h);
        }
    }
}