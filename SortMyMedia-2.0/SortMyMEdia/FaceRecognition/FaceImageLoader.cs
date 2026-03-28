using OpenCvSharp;
using ImageMagick;
using System;
using System.IO;

namespace SortMyMedia.FaceRecognition
{
    public class FaceImageLoader
    {
        public Mat Load(string path)
        {
            Mat img = Cv2.ImRead(path, ImreadModes.Color);
            if (!img.Empty())
                return FixOrientation(img, path);

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".heic" && ext != ".heif")
                return img;

            try
            {
                using var magick = new MagickImage(path);
                magick.Format = MagickFormat.Bmp;
                byte[] bytes = magick.ToByteArray();
                Mat converted = Cv2.ImDecode(bytes, ImreadModes.Color);

                if (!converted.Empty())
                    return FixOrientation(converted, path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HEIC load failed: {ex.Message}");
            }

            return img;
        }

        private Mat FixOrientation(Mat img, string path)
        {
            try
            {
                using var magick = new MagickImage(path);
                if (magick.Orientation == OrientationType.Undefined ||
                    magick.Orientation == OrientationType.TopLeft)
                    return img;

                magick.AutoOrient();
                byte[] bytes = magick.ToByteArray(MagickFormat.Bmp);
                return Cv2.ImDecode(bytes, ImreadModes.Color);
            }
            catch
            {
                return img;
            }
        }
    }
}