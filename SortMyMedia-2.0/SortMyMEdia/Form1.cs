using OpenCvSharp;
using SortMyMedia.Engines;
using SortMyMedia.FaceRecognition;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SortMyMedia
{
    public partial class Form1 : Form
    {
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private System.Windows.Forms.Timer uiTimer;
        private int ocrProcessedCount = 0;

        private FaceRecognitionEngine? faceEngine;

        public Form1()
        {
            InitializeComponent();

            Icon = AppIconHelper.CreateAppIcon();

            // UI fixes
            labelOcr.AutoSize = true;
            labelOcr.TextAlign = ContentAlignment.MiddleLeft;

            SetupUiTimer();

            this.Text = "SortMyMedia 2.0";

            comboSortMode.Items.Add("Person Mode (Faces)");
            comboSortMode.SelectedIndex = 0;

            UpdateOcrVisibility();
            checkBoxEnableOcr.CheckedChanged += (s, e) => UpdateOcrVisibility();

            ApplyModeSpecificUi();
            AppLog.Write = msg => Log(msg);
        }

        private void Log(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
                logQueue.Enqueue(message);
        }

        private void ApplyModeSpecificUi()
        {
            if (AppModeState.CurrentMode == AppMode.MediaSorter)
            {
                checkBoxEnableOcr.Checked = false;
                checkBoxEnableOcr.Visible = false;
                checkBoxHtmlGenerator.Visible = false;
                progressBarOcr.Visible = false;
                labelOcr.Visible = false;

                labelOcr.Text = "OCR progress";

                int mediaPersonModeIndex = comboSortMode.Items
                    .Cast<object>()
                    .Select((item, index) => new { item, index })
                    .Where(x => (x.item?.ToString() ?? string.Empty).Contains("Person Mode", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.index)
                    .DefaultIfEmpty(-1)
                    .First();

                if (mediaPersonModeIndex >= 0)
                    comboSortMode.Items.RemoveAt(mediaPersonModeIndex);

                if (comboSortMode.Items.Count > 0 && comboSortMode.SelectedIndex < 0)
                    comboSortMode.SelectedIndex = 0;

                return;
            }

            if (AppModeState.CurrentMode == AppMode.OCR)
            {
                Text = "SortMyMedia 2.0 – OCR Mode";

                checkBoxEnableOcr.Visible = false;
                checkBoxHtmlGenerator.Visible = true;
                progressBarOcr.Visible = false;
                labelOcr.Visible = false;

                labelOcr.Text = "OCR progress";

                labelSortMode.Visible = false;
                comboSortMode.Visible = false;

                int ocrPersonModeIndex = comboSortMode.Items
                    .Cast<object>()
                    .Select((item, index) => new { item, index })
                    .Where(x => (x.item?.ToString() ?? string.Empty).Contains("Person Mode", StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.index)
                    .DefaultIfEmpty(-1)
                    .First();

                if (ocrPersonModeIndex >= 0)
                    comboSortMode.Items.RemoveAt(ocrPersonModeIndex);

                return;
            }

            if (AppModeState.CurrentMode == AppMode.FaceGrouping)
            {
                Text = "SortMyMedia 2.0 – Face Mode";

                checkBoxEnableOcr.Visible = false;
                checkBoxHtmlGenerator.Visible = false;
                progressBarOcr.Visible = true;
                labelOcr.Visible = true;
                labelOcr.Text = "Cluster progress";

                progressBarOcr.Visible = false;
                labelOcr.Visible = false;
                progressBarOcr.Value = 0;

                labelSortMode.Visible = true;
                comboSortMode.Visible = true;
                comboSortMode.Items.Clear();
                comboSortMode.Items.AddRange(new object[]
                {
                    "Per person",
                    "Per person → per day",
                    "Per person → per month"
                });
                comboSortMode.SelectedIndex = 0;
            }
        }

        private void UpdateOcrVisibility()
        {
            if (AppModeState.CurrentMode != AppMode.MediaSorter)
                return;

            progressBarOcr.Visible = checkBoxEnableOcr.Checked;
            labelOcr.Visible = checkBoxEnableOcr.Checked;
        }

        private void SetupUiTimer()
        {
            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 100;
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (stopwatch.IsRunning)
                lblTimer.Text = "Elapsed: " + stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

            const int maxLinesPerTick = 200;
            int processed = 0;

            listBox1.BeginUpdate();
            try
            {
                while (processed < maxLinesPerTick && logQueue.TryDequeue(out string line))
                {
                    if (line != null)
                    {
                        listBox1.Items.Add(line);
                        processed++;
                    }
                }

                if (processed > 0)
                    listBox1.TopIndex = listBox1.Items.Count - 1;
            }
            finally
            {
                listBox1.EndUpdate();
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                textBox1.Text = dialog.SelectedPath;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
                textBox2.Text = dialog.SelectedPath;
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (!System.IO.Directory.Exists(textBox1.Text))
            {
                MessageBox.Show("Input folder does not exist.");
                return;
            }

            if (string.IsNullOrWhiteSpace(textBox2.Text))
            {
                MessageBox.Show("Please select an output folder.");
                return;
            }

            listBox1.Items.Clear();
            progressBar1.Value = 0;
            progressBarOcr.Value = 0;
            ocrProcessedCount = 0;
            button3.Enabled = false;

            stopwatch.Reset();
            stopwatch.Start();
            lblTimer.Text = "Elapsed: 00:00:00";

            string input = textBox1.Text;
            string output = textBox2.Text;

            int mode = comboSortMode.SelectedIndex;
            string selectedMode = comboSortMode.SelectedItem?.ToString() ?? string.Empty;

            AppMode currentMode = AppModeState.CurrentMode;
            bool isPersonMode = currentMode == AppMode.FaceGrouping;
            bool ocrRequested = currentMode == AppMode.OCR
                || (currentMode == AppMode.MediaSorter && checkBoxEnableOcr.Checked);

            // PERSON MODE
            if (isPersonMode)
            {
                await RunPersonModeAsync(input, output);
                stopwatch.Stop();
                lblTimer.Text = "Elapsed: " + stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                progressBar1.Value = 100;
                button3.Enabled = true;
                return;
            }

            // --- OCR MODE (pure OCR, no sorting) ---
            if (currentMode == AppMode.OCR)
            {
                if (!await EnsureOcrServerRunningAsync())
                {
                    button3.Enabled = true;
                    return;
                }

                var (ocrModeProcessedCount, resourceReport) = await RunPureOcrModeAsync(input, output);

                stopwatch.Stop();
                lblTimer.Text = "Elapsed: " + stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
                progressBar1.Value = 100;

                var ocrSummary = new SummaryForm(
                    ocrModeProcessedCount,
                    ocrModeProcessedCount,
                    0,
                    stopwatch.Elapsed.ToString(@"hh\:mm\:ss"));
                ocrSummary.EnableOcrDetails(ocrModeProcessedCount);
                if (resourceReport != null)
                    ocrSummary.EnableResourceDetails(resourceReport);
                ocrSummary.ShowDialog();

                button3.Enabled = true;
                return;
            }

            // KLASSIEKE MODES
            var sortMode = selectedMode.Contains("Month", StringComparison.OrdinalIgnoreCase)
                ? ClassicEngine.SortMode.PerMonth
                : ClassicEngine.SortMode.PerDay;

            IProcessingEngine engine = new ClassicEngine(sortMode);

            // SORTEREN
            var summaryData = await RunSortingAsync(engine, input, output);

            // OCR SERVER STARTEN
            if (ocrRequested)
            {
                if (await EnsureOcrServerRunningAsync())
                {
                    labelOcr.Text = "OCR progress";
                    labelOcr.Refresh();

                    await RunOcrPhaseAsync(output);
                }
            }

            stopwatch.Stop();
            lblTimer.Text = "Elapsed: " + stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

            var summary = new SummaryForm(
                summaryData.Total,
                summaryData.Photos,
                summaryData.Videos,
                stopwatch.Elapsed.ToString(@"hh\:mm\:ss")
            );
            summary.ShowDialog();

            button3.Enabled = true;
        }

        private async Task<ProcessingSummary> RunSortingAsync(IProcessingEngine engine, string input, string output)
        {
            return await Task.Run(() =>
                engine.Process(
                    input,
                    output,
                    log => logQueue.Enqueue(log),
                    pct => this.BeginInvoke(new Action(() => progressBar1.Value = pct))
                )
            );
        }

        private async Task RunOcrPhaseAsync(string outputFolder)
        {
            var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp",
                ".heic", ".heif", ".bmp"
            };

            var files = System.IO.Directory.EnumerateFiles(outputFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => imageExts.Contains(Path.GetExtension(f)))
                .ToList();

            if (files.Count == 0)
            {
                logQueue.Enqueue("OCR phase skipped: no images found in output.");
                return;
            }

            logQueue.Enqueue($"OCR phase started: {files.Count} images…");

            int processed = 0;

            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (file, ct) =>
            {
                await OcrHelper.TryRunOcrAsync(outputFolder, file, msg => logQueue.Enqueue(msg));

                string ocrFile = Path.Combine(
                    outputFolder,
                    "ocr",
                    Path.GetFileNameWithoutExtension(file) + ".txt"
                );

                if (File.Exists(ocrFile))
                {
                    Interlocked.Increment(ref ocrProcessedCount);
                }

                int pct = (int)(Interlocked.Increment(ref processed) / (double)files.Count * 100);
                this.BeginInvoke(new Action(() => progressBarOcr.Value = pct));
            });

            logQueue.Enqueue("OCR phase completed.");
        }

        private async Task<(int Processed, ResourceUsageReport? ResourceReport)> RunPureOcrModeAsync(string inputFolder, string outputFolder)
        {
            var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp",
                ".heic", ".heif", ".bmp"
            };

            var files = System.IO.Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => imageExts.Contains(Path.GetExtension(f)))
                .ToList();

            if (files.Count == 0)
            {
                logQueue.Enqueue("OCR mode: no images found.");
                return (0, null);
            }

            // Read HTML generator checkbox state on UI thread before entering background work
            bool htmlEnabled = checkBoxHtmlGenerator.Checked;

            logQueue.Enqueue($"OCR mode started: {files.Count} images…" +
                (htmlEnabled ? " (HTML generator enabled)" : ""));
            logQueue.Enqueue($"Resource monitoring active (CPU cores: {Environment.ProcessorCount}, parallelism: 4)");

            int processed = 0;

            using var monitor = new ResourceMonitor(intervalMs: 500);
            monitor.Start();

            await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (file, ct) =>
            {
                // When HTML generator is enabled, use the extended method that
                // produces both .txt and .html per image; otherwise keep existing behavior.
                if (htmlEnabled)
                    await OcrHelper.TryRunOcrWithHtmlAsync(outputFolder, file, true, msg => logQueue.Enqueue(msg));
                else
                    await OcrHelper.TryRunOcrAsync(outputFolder, file, msg => logQueue.Enqueue(msg));

                int pct = (int)(Interlocked.Increment(ref processed) / (double)files.Count * 100);
                this.BeginInvoke(new Action(() => progressBar1.Value = pct));
            });

            var report = monitor.Stop();

            logQueue.Enqueue("OCR mode completed.");
            logQueue.Enqueue("─── Resource Usage Report ───");
            logQueue.Enqueue($"  CPU  → avg {report.AvgCpuPercent:F1}%  max {report.MaxCpuPercent:F1}%");
            logQueue.Enqueue($"  RAM  → avg {report.AvgMemoryMB:F0} MB  max {report.MaxMemoryMB:F0} MB");
            if (report.GpuAvailable)
            {
                logQueue.Enqueue($"  GPU  → avg {report.AvgGpuPercent:F1}%  max {report.MaxGpuPercent:F1}%");
                logQueue.Enqueue($"  VRAM → avg {report.AvgGpuMemoryPercent:F1}%  max {report.MaxGpuMemoryPercent:F1}%  ({report.GpuMemoryUsedMB:F0}/{report.GpuMemoryTotalMB:F0} MB)");
            }
            else
            {
                logQueue.Enqueue("  GPU  → not available (nvidia-smi not found)");
            }
            logQueue.Enqueue($"  Samples collected: {report.SampleCount}");
            logQueue.Enqueue("─────────────────────────────");

            return (processed, report);
        }

        private async Task<bool> EnsureOcrServerRunningAsync()
        {
            if (await OcrHelper.IsServerRunningAsync())
                return true;

            // Resolve the server script path (UI thread for dialog)
            string? scriptPath = OcrHelper.ResolveServerScriptPath();

            if (string.IsNullOrEmpty(scriptPath))
            {
                using var dialog = new OpenFileDialog
                {
                    Title = "Locate OCR server start script",
                    Filter = "Batch files (*.bat)|*.bat|All files (*.*)|*.*",
                    FileName = "start_ocr_server.bat"
                };

                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    MessageBox.Show("No OCR server script selected.",
                        "SortMyMedia 2.0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                scriptPath = dialog.FileName;
                OcrHelper.SaveServerScriptPath(scriptPath);
            }

            labelOcr.Visible = true;
            labelOcr.Text = "Starting OCR server...";
            labelOcr.Refresh();
            logQueue.Enqueue($"Starting OCR server: {scriptPath}");

            OcrHelper.StartOcrServer(scriptPath);

            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(500);
                if (await OcrHelper.IsServerRunningAsync())
                {
                    logQueue.Enqueue("OCR server is ready.");
                    return true;
                }

                if (i % 4 == 3)
                    logQueue.Enqueue($"Waiting for OCR server... ({(i + 1) / 2}s)");
            }

            MessageBox.Show("OCR server failed to start after 30 seconds.\n\nCheck that the script works correctly.",
                "SortMyMedia 2.0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // PERSON MODE
        private async Task RunPersonModeAsync(string inputFolder, string outputFolder)
        {
            if (!EnsureFaceEngineInitialized())
                return;

            this.BeginInvoke(new Action(() =>
            {
                progressBarOcr.Value = 0;
                progressBarOcr.Visible = false;
                labelOcr.Visible = false;
                labelOcr.Text = "Cluster progress";
            }));

            logQueue.Enqueue("Person Mode gestart…");
            bool clusteringPhaseStarted = false;

            var imageExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif"
            };

            var files = System.IO.Directory.EnumerateFiles(inputFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => imageExts.Contains(Path.GetExtension(f)))
                .ToList();

            if (files.Count == 0)
            {
                logQueue.Enqueue("Geen afbeeldingen gevonden.");
                return;
            }

            System.IO.Directory.CreateDirectory(outputFolder);

            string selectedStructure = comboSortMode.SelectedItem?.ToString() ?? "Per person";
            bool useStructuredOutput = !string.Equals(selectedStructure, "Per person", StringComparison.Ordinal);
            string processingOutputFolder = outputFolder;
            string tempFaceOutputFolder = Path.Combine(outputFolder, $".face_mode_{Guid.NewGuid():N}");

            if (useStructuredOutput)
                processingOutputFolder = tempFaceOutputFolder;

            await faceEngine!.ProcessPersonModeAsync(
                files,
                processingOutputFolder,
                msg =>
                {
                    if (!clusteringPhaseStarted && string.Equals(msg, "Clustering…", StringComparison.Ordinal))
                    {
                        clusteringPhaseStarted = true;
                        logQueue.Enqueue("Face detection completed. Building clusters...");
                        this.BeginInvoke(new Action(() =>
                        {
                            labelFileProgress.Text = "Building clusters...";
                            progressBar1.Style = ProgressBarStyle.Blocks;
                            progressBar1.MarqueeAnimationSpeed = 0;
                            progressBar1.Value = 100;
                            labelOcr.Text = "Cluster progress";
                            progressBarOcr.Value = 0;
                            progressBarOcr.Visible = true;
                            labelOcr.Visible = true;
                            labelFileProgress.Refresh();
                            progressBar1.Refresh();
                            Refresh();
                        }));
                    }

                    logQueue.Enqueue(msg);
                },
                pct => this.BeginInvoke(new Action(() => progressBar1.Value = pct)));

            if (useStructuredOutput)
            {
                progressBar1.Style = ProgressBarStyle.Blocks;
                progressBar1.MarqueeAnimationSpeed = 0;
                progressBar1.Value = 0;
                labelFileProgress.Text = "sorting to date...";
                labelFileProgress.Refresh();
                progressBar1.Refresh();
                Refresh();

                await Task.Run(() =>
                    ApplyStructuredFaceOutput(
                        processingOutputFolder,
                        outputFolder,
                        selectedStructure,
                        files,
                        msg => logQueue.Enqueue(msg),
                        pct => this.BeginInvoke(new Action(() => progressBar1.Value = pct))));

                labelFileProgress.Text = "date sorting completed.";
                labelFileProgress.Refresh();
                progressBar1.Value = 100;
            }

            logQueue.Enqueue("Person Mode voltooid.");
            this.BeginInvoke(new Action(() =>
            {
                progressBar1.Style = ProgressBarStyle.Blocks;
                progressBar1.MarqueeAnimationSpeed = 0;
                labelFileProgress.Text = useStructuredOutput ? "date sorting completed." : "File progress";
                progressBar1.Value = 100;
                progressBarOcr.Value = 100;
                labelOcr.Text = "Cluster progress";
            }));
        }

        private void ApplyStructuredFaceOutput(
            string sourceRoot,
            string outputFolder,
            string selectedStructure,
            IReadOnlyList<string> sourceFiles,
            Action<string> log,
            Action<int> progress)
        {
            var sourceFilesByName = sourceFiles
                .GroupBy(file => Path.GetFileName(file) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new Queue<string>(g),
                    StringComparer.OrdinalIgnoreCase);

            var filesToSort = System.IO.Directory.EnumerateDirectories(sourceRoot)
                .SelectMany(personDir => System.IO.Directory.EnumerateFiles(personDir).Select(file => (personDir, file)))
                .ToList();

            int total = filesToSort.Count;
            if (total == 0)
            {
                log("Face Mode date sorting: no files to sort.");
                progress(100);
            }

            int processed = 0;

            foreach (var item in filesToSort)
            {
                string personName = Path.GetFileName(item.personDir);
                string file = item.file;
                string originalPath = file;
                string fileName = Path.GetFileName(file);
                if (sourceFilesByName.TryGetValue(fileName, out Queue<string>? originals) && originals.Count > 0)
                    originalPath = originals.Dequeue();

                DateTime? fileDate = ResolveMediaSorterDate(originalPath);
                string periodFolder = fileDate.HasValue
                    ? (string.Equals(selectedStructure, "Per person → per day", StringComparison.Ordinal)
                        ? fileDate.Value.ToString("yyyy-MM-dd")
                        : fileDate.Value.ToString("yyyy-MM"))
                    : "NO_DATE";

                string targetDir = Path.Combine(outputFolder, personName, periodFolder);
                System.IO.Directory.CreateDirectory(targetDir);

                string targetPath = Path.Combine(targetDir, Path.GetFileName(file));
                if (!File.Exists(targetPath))
                    File.Move(file, targetPath);

                if (fileDate.HasValue)
                    log($"Face Mode date sorting: {Path.GetFileName(file)} → {personName}\\{periodFolder}");
                else
                    log($"Face Mode date sorting: {Path.GetFileName(file)} → {personName}\\NO_DATE (no valid date found)");

                processed++;
                int pct = total == 0 ? 100 : (int)(processed / (double)total * 100);
                progress(pct);
            }

            if (System.IO.Directory.Exists(sourceRoot))
                System.IO.Directory.Delete(sourceRoot, true);
        }

        private static DateTime? ResolveMediaSorterDate(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool isHeic = ext == ".heic" || ext == ".heif";

            DateTime? date = isHeic ? GetHeicDateViaExifTool(filePath) : GetMetadataDate(filePath);

            bool invalidDate = date == null || date.Value.Year < 1900 || date.Value.Year == 1904;
            if (invalidDate && TryGetDateFromJson(filePath, out DateTime jsonDate))
                date = jsonDate;

            invalidDate = date == null || date.Value.Year < 1900 || date.Value.Year == 1904;
            if (invalidDate && TryGetDateFromFileName(Path.GetFileNameWithoutExtension(filePath), out DateTime fileNameDate))
                date = fileNameDate;

            if (date == null || date.Value.Year < 1900 || date.Value.Year == 1904)
                return null;

            return date;
        }

        private static bool TryGetDateFromFileName(string fileNameWithoutExtension, out DateTime date)
        {
            date = DateTime.MinValue;

            var patterns = new[]
            {
                @"(?<!\d)(\d{4})[-_]?([01]\d)[-_]?([0-3]\d)(?!\d)",
                @"(?<!\d)(\d{8})(?!\d)"
            };

            foreach (string pattern in patterns)
            {
                Match match = Regex.Match(fileNameWithoutExtension, pattern);
                if (!match.Success)
                    continue;

                string candidate = match.Groups.Count >= 4
                    ? $"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}"
                    : match.Groups[1].Value;

                if (DateTime.TryParseExact(candidate,
                    new[] { "yyyy-MM-dd", "yyyyMMdd" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
                {
                    date = parsed;
                    return true;
                }
            }

            return false;
        }

        private static string? FindJsonByPrefix(string filePath)
        {
            string? folder = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
                return null;

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

        private static bool TryGetDateFromJson(string filePath, out DateTime date)
        {
            date = DateTime.MinValue;

            string? jsonPath = FindJsonByPrefix(filePath);
            if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
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

        private static DateTime? GetHeicDateViaExifTool(string filePath)
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
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var p = Process.Start(psi);
                if (p == null)
                    return null;

                string? output = p.StandardOutput.ReadToEnd()?.Trim();
                if (string.IsNullOrWhiteSpace(output))
                    return null;

                if (DateTime.TryParseExact(output,
                    "yyyy:MM:dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime parsed))
                {
                    return parsed;
                }
            }
            catch
            {
            }

            return null;
        }

        private static DateTime? GetMetadataDate(string filePath)
        {
            try
            {
                var dirs = MetadataExtractor.ImageMetadataReader.ReadMetadata(filePath);

                var exif = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exif != null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime exifDate))
                    return exifDate;

                var qt = dirs.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (qt != null && qt.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out DateTime qtDate))
                    return qtDate;

                var qt2 = dirs.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
                if (qt2 != null && qt2.TryGetDateTime(QuickTimeMetadataHeaderDirectory.TagCreationDate, out DateTime qtDate2))
                    return qtDate2;
            }
            catch
            {
            }

            return null;
        }

        private bool EnsureFaceEngineInitialized()
        {
            if (faceEngine != null)
                return true;

            try
            {
                var faceOptions = new FaceRecognitionPipelineOptions
                {
                    ClusterDistanceThreshold = 0.40f,
                    MinClusterSize = 2
                };

                faceEngine = new FaceRecognitionEngine(faceOptions);
                faceEngine.OnChunkProgress += HandleClusterChunkProgress;
                faceEngine.OnGlobalMergeProgress += HandleClusterGlobalMergeProgress;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Person Mode kon niet starten: {ex.Message}",
                    "SortMyMedia 2.0",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                logQueue.Enqueue($"Person Mode initialisatie mislukt: {ex.Message}");
                return false;
            }
        }

        private void HandleClusterChunkProgress(int completed, int total)
        {
            UpdateClusteringProgress(completed, total, 0.0, 0.70, "Cluster progress");
        }

        private void HandleClusterGlobalMergeProgress(int completed, int total)
        {
            UpdateClusteringProgress(completed, total, 0.70, 0.30, "Cluster progress");
        }

        private void UpdateClusteringProgress(int completed, int total, double phaseStart, double phaseSpan, string label)
        {
            if (AppModeState.CurrentMode != AppMode.FaceGrouping)
                return;

            double ratio = total <= 0 ? 1.0 : Math.Clamp(completed / (double)total, 0.0, 1.0);
            int pct = (int)Math.Round((phaseStart + (ratio * phaseSpan)) * 100.0);
            pct = Math.Clamp(pct, 0, 100);

            this.BeginInvoke(new Action(() =>
            {
                if (progressBarOcr.Style != ProgressBarStyle.Blocks)
                    progressBarOcr.Style = ProgressBarStyle.Blocks;

                labelOcr.Text = label;
                progressBarOcr.Value = pct;
            }));
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            AppLog.Write = null;

            if (faceEngine != null)
            {
                faceEngine.OnChunkProgress -= HandleClusterChunkProgress;
                faceEngine.OnGlobalMergeProgress -= HandleClusterGlobalMergeProgress;
                faceEngine.Dispose();
                faceEngine = null;
            }

            base.OnFormClosed(e);
        }
    }
}