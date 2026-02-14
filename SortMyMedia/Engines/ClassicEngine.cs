using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace SortMyMedia.Engines
{
    public class ClassicEngine : IProcessingEngine
    {
        private enum SortMode { PerDay, PerMonth }

        private SortMode currentSortMode = SortMode.PerDay;

        public void Process(
            string inputFolder,
            string outputFolder,
            Action<string> log,
            Action<int> progress)
        {
            ProcessAllFiles(inputFolder, outputFolder, log, progress);
        }

        private void ProcessAllFiles(
            string inputFolder,
            string outputFolder,
            Action<string> log,
            Action<int> progress)
        {
            string[] exts = new[]
            {
                ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp",
                ".mp4", ".mov", ".m4v",
                ".heic", ".heif"
            };

            var files = System.IO.Directory.GetFiles(inputFolder, "*.*", SearchOption.AllDirectories)
                                 .Where(f => exts.Contains(Path.GetExtension(f).ToLower()))
                                 .ToList();

            int totalFiles = files.Count;
            int processedFiles = 0;

            log($"Processing all files in C# (multithreaded)… ({totalFiles} files)");

            Parallel.ForEach(files, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            file =>
            {
                try
                {
                    ProcessSingleFile(file, outputFolder, log);
                }
                catch (Exception ex)
                {
                    log($"ERROR in {Path.GetFileName(file)}: {ex.Message}");
                }

                int pct = (int)((Interlocked.Increment(ref processedFiles) / (double)totalFiles) * 100);
                progress(pct);
            });

            log("All processing done.");
        }

        private void ProcessSingleFile(string filePath, string outputFolder, Action<string> log)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            bool isVideo = ext == ".mp4" || ext == ".mov" || ext == ".m4v";
            bool isHeic = ext == ".heic" || ext == ".heif";

            DateTime? date = isHeic ? GetHeicDateViaExifTool(filePath) : GetMetadataDate(filePath);

            if (date == null)
            {
                string noDateFolder = Path.Combine(outputFolder, "NO_DATE");
                System.IO.Directory.CreateDirectory(noDateFolder);
                File.Copy(filePath, Path.Combine(noDateFolder, Path.GetFileName(filePath)), true);

                log($"NO DATE → {Path.GetFileName(filePath)}");
                return;
            }

            string typeFolder = isVideo ? "videos" : "photos";
            string year = date.Value.ToString("yyyy");

            string subfolder = currentSortMode == SortMode.PerMonth
                ? date.Value.ToString("yyyy-MM")
                : date.Value.ToString("yyyy-MM-dd");

            string targetFolder = Path.Combine(outputFolder, typeFolder, year, subfolder);
            System.IO.Directory.CreateDirectory(targetFolder);

            File.Copy(filePath, Path.Combine(targetFolder, Path.GetFileName(filePath)), true);

            log($"{Path.GetFileName(filePath)} → {typeFolder}\\{year}\\{subfolder}");
        }

        private DateTime? GetHeicDateViaExifTool(string filePath)
        {
            try
            {
                string exifToolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exiftool.exe");

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = exifToolPath,
                    Arguments = $"-s -s -s -DateTimeOriginal \"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (System.Diagnostics.Process? p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null)
                        return null;

                    string? output = p.StandardOutput.ReadToEnd()?.Trim();

                    if (string.IsNullOrWhiteSpace(output))
                        return null;

                    if (DateTime.TryParseExact(
                        output,
                        "yyyy:MM:dd HH:mm:ss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out DateTime parsed))
                    {
                        return parsed;
                    }

                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private DateTime? GetMetadataDate(string filePath)
        {
            try
            {
                var dirs = ImageMetadataReader.ReadMetadata(filePath);

                var exif = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exif != null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime exifDate))
                    return exifDate;

                var qt = dirs.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (qt != null && qt.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out DateTime qtDate))
                    return qtDate;

                var qt2 = dirs.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
                if (qt2 != null && qt2.TryGetDateTime(QuickTimeMetadataHeaderDirectory.TagCreationDate, out DateTime qtDate2))
                    return qtDate2;

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}