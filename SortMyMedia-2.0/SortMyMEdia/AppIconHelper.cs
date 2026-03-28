using System.Drawing;
using System.Drawing.Drawing2D;

namespace SortMyMedia
{
    internal static class AppIconHelper
    {
        /// <summary>
        /// Creates the application icon from the resource image, cropped to the
        /// camera content so the graphic fills the maximum available icon area.
        /// </summary>
        public static Icon CreateAppIcon()
        {
            using var source = Properties.Resources.image_1_1770914179217;

            // The source image is 1024×1024 but the camera graphic occupies
            // roughly (48,104)–(968,972). Crop to a centered square around
            // that content so the camera fills the icon at any size.
            const int cropX = 38;
            const int cropY = 68;
            const int cropSize = 940;

            using var icon = new Bitmap(256, 256);
            using (var g = Graphics.FromImage(icon))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source,
                    new Rectangle(0, 0, 256, 256),
                    new Rectangle(cropX, cropY, cropSize, cropSize),
                    GraphicsUnit.Pixel);
            }

            return Icon.FromHandle(icon.GetHicon());
        }
    }
}
