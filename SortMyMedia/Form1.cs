using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace SortMyMedia
{
    public partial class Form1 : Form
    {
        private enum SortMode { PerDay, PerMonth }

        private int totalFiles = 0;
        private int processedFiles = 0;

        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private System.Windows.Forms.Timer? uiTimer;
        private SortMode currentSortMode = SortMode.PerDay;

        public Form1()
        {
            InitializeComponent();
            SetupUiTimer();

            this.Text = "SortMyMedia";


            // default: Per dag
            comboSortMode.SelectedIndex = 0;
        }

        private void SetupUiTimer()
        {
            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 100;
            uiTimer.Tick += UiTimer_Tick;
            uiTimer.Start();
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            while (logQueue.TryDequeue(out string? line))
            {
                if (line != null)
                {
                    listBox1.Items.Add(line);
                    listBox1.TopIndex = listBox1.Items.Count - 1;
                }
            }

            if (totalFiles > 0)
            {
                int pct = (int)((processedFiles / (double)totalFiles) * 100);
                if (pct > 100) pct = 100;
                progressBar1.Value = pct;
            }
        }

        private void comboSortMode_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (comboSortMode.SelectedIndex == 0)
                currentSortMode = SortMode.PerDay;
            else
                currentSortMode = SortMode.PerMonth;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                    textBox1.Text = dialog.SelectedPath;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                    textBox2.Text = dialog.SelectedPath;
            }
        }

        private void button3_Click(object sender, EventArgs e)
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
            button3.Enabled = false;

            string input = textBox1.Text;
            string output = textBox2.Text;

            Task.Run(() =>
            {
                ProcessAllFiles(input, output);

                this.Invoke(new Action(() =>
                {
                    button3.Enabled = true;
                }));
            });
        }

        // -----------------------------
        // ALLE BESTANDEN (C# ONLY)
        // -----------------------------
        private void ProcessAllFiles(string inputFolder, string outputFolder)
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

            totalFiles = files.Count;
            processedFiles = 0;

            logQueue.Enqueue($"Processing all files in C# (multithreaded)… ({totalFiles} files)");

            Parallel.ForEach(files, new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            file =>
            {
                try
                {
                    ProcessSingleFile(file, outputFolder);
                }
                catch (Exception ex)
                {
                    logQueue.Enqueue($"ERROR in {Path.GetFileName(file)}: {ex.Message}");
                }
            });

            logQueue.Enqueue("All processing done.");
        }

        private void ProcessSingleFile(string filePath, string outputFolder)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            bool isVideo = ext == ".mp4" || ext == ".mov" || ext == ".m4v";
            bool isHeic = ext == ".heic" || ext == ".heif";

            DateTime? date = null;

            if (isHeic)
                date = GetHeicDateViaExifTool(filePath);
            else
                date = GetMetadataDate(filePath);

            if (date == null)
            {
                string noDateFolder = Path.Combine(outputFolder, "NO_DATE");
                System.IO.Directory.CreateDirectory(noDateFolder);
                File.Copy(filePath, Path.Combine(noDateFolder, Path.GetFileName(filePath)), true);

                logQueue.Enqueue($"NO DATE → {Path.GetFileName(filePath)}");
            }
            else
            {
                string typeFolder = isVideo ? "videos" : "photos";
                string year = date.Value.ToString("yyyy");

                string subfolder = currentSortMode == SortMode.PerMonth
                    ? date.Value.ToString("yyyy-MM")
                    : date.Value.ToString("yyyy-MM-dd");

                string targetFolder = Path.Combine(outputFolder, typeFolder, year, subfolder);
                System.IO.Directory.CreateDirectory(targetFolder);

                File.Copy(filePath, Path.Combine(targetFolder, Path.GetFileName(filePath)), true);

                logQueue.Enqueue($"{Path.GetFileName(filePath)} → {typeFolder}\\{year}\\{subfolder}");
            }

            Interlocked.Increment(ref processedFiles);
        }

        // -----------------------------
        // HEIC VIA EXIFTOOL
        // -----------------------------
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

                using (Process? p = Process.Start(psi))
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

        // -----------------------------
        // FOTO/VIDEO METADATA
        // -----------------------------
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

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}