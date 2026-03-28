using System;
using System.Drawing;
using System.Windows.Forms;

namespace SortMyMedia
{
    public partial class SummaryForm : Form
    {
        private const int LineSpacing = 50;
        private const int ResourceLineSpacing = 28;

        private Label? lblResourceHeader;
        private Label? lblCpuUsage;
        private Label? lblMemUsage;
        private Label? lblGpuUsage;
        private Label? lblVramUsage;

        public SummaryForm(int total, int photos, int videos, string elapsed)
        {
            InitializeComponent();

            Icon = AppIconHelper.CreateAppIcon();

            lblTotal.Text = "Total files: " + total;
            lblPhotos.Text = "Photos: " + photos;
            lblVideos.Text = "Videos: " + videos;
            lblTime.Text = "Elapsed time: " + elapsed;

            SetOcrFieldsVisible(false);
            ApplyLayout();
        }

        public void EnableOcrDetails(int ocrProcessed, string? deviceUsed = null)
        {
            lblOcr.Text = "OCR processed: " + ocrProcessed;

            string resolvedDevice = deviceUsed;
            if (string.IsNullOrWhiteSpace(resolvedDevice))
                resolvedDevice = OcrHelper.LastDeviceUsed.StartsWith("gpu", StringComparison.OrdinalIgnoreCase) ? "GPU" : "CPU";

            lblDevice.Text = "OCR device used: " + resolvedDevice;
            SetOcrFieldsVisible(true);
            ApplyLayout();
        }

        public void EnableResourceDetails(ResourceUsageReport report)
        {
            var resourceFont = new Font("Segoe UI", 11F);
            int contentLeft = lblVideos.Left;

            lblResourceHeader = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 13F, FontStyle.Bold),
                Text = "── Resource Utilization ──",
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            Controls.Add(lblResourceHeader);

            lblCpuUsage = new Label
            {
                AutoSize = true,
                Font = resourceFont,
                Text = $"CPU:   avg {report.AvgCpuPercent:F1}%   max {report.MaxCpuPercent:F1}%"
            };
            Controls.Add(lblCpuUsage);

            lblMemUsage = new Label
            {
                AutoSize = true,
                Font = resourceFont,
                Text = $"RAM:   avg {report.AvgMemoryMB:F0} MB   max {report.MaxMemoryMB:F0} MB"
            };
            Controls.Add(lblMemUsage);

            if (report.GpuAvailable)
            {
                lblGpuUsage = new Label
                {
                    AutoSize = true,
                    Font = resourceFont,
                    Text = $"GPU:   avg {report.AvgGpuPercent:F1}%   max {report.MaxGpuPercent:F1}%"
                };
                Controls.Add(lblGpuUsage);

                lblVramUsage = new Label
                {
                    AutoSize = true,
                    Font = resourceFont,
                    Text = $"VRAM:  avg {report.AvgGpuMemoryPercent:F1}%   max {report.MaxGpuMemoryPercent:F1}%   ({report.GpuMemoryUsedMB:F0}/{report.GpuMemoryTotalMB:F0} MB)"
                };
                Controls.Add(lblVramUsage);
            }

            ApplyLayout();
        }

        private void SetOcrFieldsVisible(bool visible)
        {
            lblOcr.Visible = visible;
            lblDevice.Visible = visible;
        }

        private void ApplyLayout()
        {
            SuspendLayout();

            int contentLeft = lblVideos.Left;
            int lineY = lblVideos.Top + LineSpacing;

            if (lblOcr.Visible)
            {
                lblOcr.Location = new Point(contentLeft, lineY);
                lineY += LineSpacing;
            }

            if (lblDevice.Visible)
            {
                lblDevice.Location = new Point(contentLeft, lineY);
                lineY += LineSpacing;
            }

            lblTime.Location = new Point(contentLeft, lineY);

            if (lblResourceHeader != null)
            {
                lineY += LineSpacing + 10;
                lblResourceHeader.Location = new Point(contentLeft, lineY);

                lineY += ResourceLineSpacing + 8;
                if (lblCpuUsage != null) { lblCpuUsage.Location = new Point(contentLeft, lineY); lineY += ResourceLineSpacing; }
                if (lblMemUsage != null) { lblMemUsage.Location = new Point(contentLeft, lineY); lineY += ResourceLineSpacing; }
                if (lblGpuUsage != null) { lblGpuUsage.Location = new Point(contentLeft, lineY); lineY += ResourceLineSpacing; }
                if (lblVramUsage != null) { lblVramUsage.Location = new Point(contentLeft, lineY); lineY += ResourceLineSpacing; }
            }

            int requiredHeight = lineY + 40;
            if (requiredHeight > ClientSize.Height)
                ClientSize = new Size(ClientSize.Width, requiredHeight);

            ResumeLayout(true);
        }
    }
}