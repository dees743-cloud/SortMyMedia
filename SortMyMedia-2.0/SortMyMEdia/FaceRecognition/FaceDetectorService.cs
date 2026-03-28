using OpenCvSharp;
using System.Linq;

namespace SortMyMedia.FaceRecognition
{
    public class FaceDetectorService : IDisposable
    {
        private readonly FaceDetectorSCRFD detector;
        private readonly int minFaceSize;

        public FaceDetectorService(int minFaceSizePx = 80, int scrfdInputSize = 640, bool enableDetailedLogging = false)
        {
            minFaceSize = minFaceSizePx;
            detector = new FaceDetectorSCRFD(scrfdInputSize, enableDetailedLogging);
        }

        public DetectionResult[] Detect(Mat img, bool filterSmall = true)
        {
            var results = detector.DetectFaces(img);
            if (!filterSmall)
                return results;

            return results
                .Where(r => r.BBox.Width >= minFaceSize && r.BBox.Height >= minFaceSize)
                .OrderByDescending(r => r.Score)
                .ToArray();
        }

        public bool IsDetectionQualityGood(DetectionResult det)
        {
            return det.BBox.Width >= minFaceSize && det.BBox.Height >= minFaceSize;
        }

        public Rect ApplyPadding(Rect box, float factor, int imgW, int imgH)
        {
            int padX = (int)(box.Width * factor);
            int padY = (int)(box.Height * factor);

            int x = Math.Max(0, box.X - padX);
            int y = Math.Max(0, box.Y - padY);
            int w = Math.Min(imgW - x, box.Width + padX * 2);
            int h = Math.Min(imgH - y, box.Height + padY * 2);

            return new Rect(x, y, w, h);
        }

        public void Dispose()
        {
            detector.Dispose();
        }
    }
}