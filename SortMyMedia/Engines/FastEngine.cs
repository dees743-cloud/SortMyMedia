using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace SortMyMedia.Engines
{
    public class FastEngine : IProcessingEngine
    {
        private BlockingCollection<(string file, DateTime? date, bool isVideo)> copyQueue =
            new BlockingCollection<(string, DateTime?, bool)>(new ConcurrentQueue<(string, DateTime?, bool)>());

        public void Process(
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

            log($"FastEngine: processing {totalFiles} files…");

            // 1) Start COPY WORKERS (I/O threads)
            Task[] copyWorkers = Enumerable.Range(0, 3).Select(_ =>
                Task.Run(() => CopyWorker(outputFolder, log))
            ).ToArray();

            // 2) Start METADATA WORKERS (CPU threads)
            Parallel.ForEach(files, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            file =>
            {
                try
                {
                    var info = ExtractMetadata(file);
                    copyQueue.Add(info);
                }
                catch (Exception ex)
                {
                    log($"ERROR metadata {Path.GetFileName(file)}: {ex.Message}");
                }

                int pct = (int)((Interlocked.Increment(ref processedFiles) / (double)totalFiles) * 100);
                progress(pct);
            });

            // 3) Close queue
            copyQueue.CompleteAdding();

            // 4) Wait for copy workers
            Task.WaitAll(copyWorkers);

            log("FastEngine: all done.");
        }

        private (string file, DateTime? date, bool isVideo) ExtractMetadata(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            bool isVideo = ext == ".mp4" || ext == ".mov" || ext == ".m4v";
            bool isHeic = ext == ".heic" || ext == ".heif";

            DateTime? date = isHeic ? GetHeicDateViaExifTool(filePath) : GetMetadataDate(filePath);

            return (filePath, date, isVideo);
        }

        private void CopyWorker(string outputFolder, Action<string> log)
        {
            foreach (var item in copyQueue.GetConsumingEnumerable())
            {
                string filePath = item.file;
                DateTime? date = item.date;
                bool isVideo = item.isVideo;

                try
                {
                    if (date == null)
                    {
                        string noDateFolder = Path.Combine(outputFolder, "NO_DATE");
                        System.IO.Directory.CreateDirectory(noDateFolder);

                        string dest = Path.Combine(noDateFolder, Path.GetFileName(filePath));
                        CopyFileFast(filePath, dest);

                        log($"NO DATE → {Path.GetFileName(filePath)}");
                        continue;
                    }

                    string typeFolder = isVideo ? "videos" : "photos";
                    string year = date.Value.ToString("yyyy");
                    string subfolder = date.Value.ToString("yyyy-MM-dd");

                    string targetFolder = Path.Combine(outputFolder, typeFolder, year, subfolder);
                    System.IO.Directory.CreateDirectory(targetFolder);

                    string destPath = Path.Combine(targetFolder, Path.GetFileName(filePath));
                    CopyFileFast(filePath, destPath);

                    log($"{Path.GetFileName(filePath)} → {typeFolder}\\{year}\\{subfolder}");
                }
                catch (Exception ex)
                {
                    log($"ERROR copy {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }
        }

        private void CopyFileFast(string source, string dest)
        {
            const int bufferSize = 128 * 1024; // 128 KB

            using (var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read,
                                            bufferSize, FileOptions.SequentialScan))
            using (var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None,
                                            bufferSize, FileOptions.WriteThrough))
            {
                src.CopyTo(dst, bufferSize);
            }
        }

        private DateTime? GetHeicDateViaExifTool(string filePath)
        {
            try
            {
                string exifToolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exiftool.exe");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exifToolPath,
                    Arguments = $"-s -s -s -DateTimeOriginal \"{filePath}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    string? output = p?.StandardOutput.ReadToEnd()?.Trim();
                    if (string.IsNullOrWhiteSpace(output))
                        return null;

                    if (DateTime.TryParseExact(output, "yyyy:MM:dd HH:mm:ss",
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