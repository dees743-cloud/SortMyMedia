using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SortMyMedia.Engines;

namespace SortMyMedia
{
    public partial class Form1 : Form
    {
        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private System.Windows.Forms.Timer stopwatchTimer = new System.Windows.Forms.Timer();
        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private System.Windows.Forms.Timer? uiTimer;
        private int totalFilesProcessed = 0;
        private int totalPhotos = 0;
        private int totalVideos = 0;

        public Form1()
        {
            InitializeComponent();
            stopwatchTimer.Interval = 1000; // 1 seconde
            stopwatchTimer.Tick += StopwatchTimer_Tick;
            SetupUiTimer();

            this.Text = "SortMyMedia";

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
                    // TELLERS UPDATEN
                    totalFilesProcessed++;

                    string lower = line.ToLower();
                    if (lower.Contains("photos") || lower.Contains("photo"))
                        totalPhotos++;
                    if (lower.Contains("videos") || lower.Contains("video"))
                        totalVideos++;
                }
            }
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
            if (!Directory.Exists(textBox1.Text))
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

            // ⭐ RESET TELERS
            totalFilesProcessed = 0;
            totalPhotos = 0;
            totalVideos = 0;

            // ⭐ START TIMER 
            stopwatch.Reset();
            stopwatch.Start();
            stopwatchTimer.Start();
            lblTimer.Text = "Elapsed: 00:00:00";
            // ⭐ EINDE TIMER CODE


            string input = textBox1.Text;
            string output = textBox2.Text;

            // ENGINE KIEZEN
            IProcessingEngine engine = new ClassicEngine();

            Task.Run(() =>
            {
                engine.Process(input, output,
                    log => logQueue.Enqueue(log),
                    pct => this.Invoke(new Action(() => progressBar1.Value = pct))
                );

                // ⭐ STOP TIMER — deze regels toevoegen
                this.Invoke(new Action(() =>
                {
                    // STOP TIMER
                    stopwatch.Stop();
                    stopwatchTimer.Stop();
                    lblTimer.Text = "Elapsed: " + stopwatch.Elapsed.ToString(@"hh\:mm\:ss");

                    // ⭐ SPLASH-SCHERM TONEN
                    var summary = new SummaryForm(
                        totalFilesProcessed,
                        totalPhotos,
                        totalVideos,
                        stopwatch.Elapsed.ToString(@"hh\:mm\:ss")
                    );

                    summary.ShowDialog();

                    button3.Enabled = true;
                }));
                // ⭐ EINDE STOP TIMER


                this.Invoke(new Action(() =>
                {
                    button3.Enabled = true;
                }));
            });
        }

        private void comboSortMode_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Wordt later gebruikt door ClassicEngine of FastEngine
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void StopwatchTimer_Tick(object? sender, EventArgs e)
        {
            lblTimer.Text = "Elapsed: " + stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        }

        private void labelSortMode_Click(object sender, EventArgs e)
        {

        }
    }
}