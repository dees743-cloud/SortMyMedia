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
using Newtonsoft.Json;

namespace SortMyMedia.Engines
{
    public class GoogleJsonMeta
    {
        public GoogleJsonTimestamp? photoTakenTime { get; set; }
        public GoogleJsonTimestamp? creationTime { get; set; }
    }

    public class GoogleJsonTimestamp
    {
        public string? timestamp { get; set; }
    }

    public class ClassicEngine : IProcessingEngine
    {
        public enum SortMode { PerDay, PerMonth }
        private readonly SortMode currentSortMode;

        private int totalFiles = 0;
        private int totalPhotos = 0;
        private int totalVideos = 0;

        public ClassicEngine(SortMode mode)
        {
            currentSortMode = mode;
        }

        public async Task<ProcessingSummary> Process(
            string inputFolder,
            string outputFolder,
            Action<string> log,
            Action<int> progress)
        {
            return await Task.Run(() =>
                ProcessAllFiles(inputFolder, outputFolder, log, progress)
            );
        }

        private ProcessingSummary ProcessAllFiles(
            string inputFolder,
            string outputFolder,
            Action<string> log,
            Action<int> progress)
        {
            var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp",
                ".mp4", ".mov", ".m4v",
                ".heic", ".heif"
            };

            var files = System.IO.Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.AllDirectories)
                                 .Where(f => exts.Contains(Path.GetExtension(f)))
                                 .ToList();

            int total = files.Count;
            int processed = 0;

            log($"Processing {total} files…");

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

                int pct = (int)((Interlocked.Increment(ref processed) / (double)total) * 100);
                progress(pct);
            });

            log("Media Sorting done.");

            return new ProcessingSummary
            {
                Total = totalFiles,
                Photos = totalPhotos,
                Videos = totalVideos
            };
        }

        private void ProcessSingleFile(string filePath, string outputFolder, Action<string> log)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            bool isVideo = ext == ".mp4" || ext == ".mov" || ext == ".m4v";
            bool isHeic = ext == ".heic" || ext == ".heif";

            Interlocked.Increment(ref totalFiles);
            if (isVideo)
                Interlocked.Increment(ref totalVideos);
            else
                Interlocked.Increment(ref totalPhotos);

            DateTime? date = isHeic ? GetHeicDateViaExifTool(filePath) : GetMetadataDate(filePath);

            bool invalidExif = date == null || date.Value.Year < 1900 || date.Value.Year == 1904;

            if (invalidExif && TryGetDateFromJson(filePath, out DateTime jsonDate))
                date = jsonDate;

            if (date == null || date.Value.Year < 1900 || date.Value.Year == 1904)
            {
                string noDateFolder = Path.Combine(outputFolder, "NO_DATE");
                System.IO.Directory.CreateDirectory(noDateFolder);
                File.Copy(filePath, Path.Combine(noDateFolder, Path.GetFileName(filePath)), true);

                log($"{Path.GetFileName(filePath)} → NO DATE");
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

        private string? FindJsonByPrefix(string filePath)
        {
            string folder = Path.GetDirectoryName(filePath)!;

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string fileNameWithExt = Path.GetFileName(filePath);

            int prefixLength = Math.Min(25, fileName.Length);
            string prefix = fileName.Substring(0, prefixLength);

            var jsonFiles = System.IO.Directory.GetFiles(folder, "*.json");

            foreach (var json in jsonFiles)
            {
                string jsonName = Path.GetFileNameWithoutExtension(json);

                if (jsonName.StartsWith(fileNameWithExt, StringComparison.OrdinalIgnoreCase))
                    return json;

                if (jsonName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    return json;

                if (jsonName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return json;
            }

            return null;
        }

        private bool TryGetDateFromJson(string filePath, out DateTime date)
        {
            date = DateTime.MinValue;

            string? jsonPath = FindJsonByPrefix(filePath);
            if (jsonPath == null || !File.Exists(jsonPath))
                return false;

            try
            {
                string json = File.ReadAllText(jsonPath);
                var data = JsonConvert.DeserializeObject<GoogleJsonMeta>(json);

                string? ts1 = data?.photoTakenTime?.timestamp?.Trim();
                if (!string.IsNullOrEmpty(ts1) && long.TryParse(ts1, out long unix1))
                {
                    date = DateTimeOffset.FromUnixTimeSeconds(unix1).DateTime;
                    return true;
                }

                string? ts2 = data?.creationTime?.timestamp?.Trim();
                if (!string.IsNullOrEmpty(ts2) && long.TryParse(ts2, out long unix2))
                {
                    date = DateTimeOffset.FromUnixTimeSeconds(unix2).DateTime;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private DateTime? GetHeicDateViaExifTool(string filePath)
        {
            try
            {
                string exifToolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exiftool.exe");

                var psi = new ProcessStartInfo
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