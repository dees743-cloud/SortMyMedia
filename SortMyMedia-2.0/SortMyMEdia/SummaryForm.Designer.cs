namespace SortMyMedia
{
    partial class SummaryForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            label1 = new Label();
            lblTotal = new Label();
            lblPhotos = new Label();
            lblVideos = new Label();
            lblOcr = new Label();
            lblTime = new Label();
            lblDevice = new Label();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 20F);
            label1.Location = new Point(279, 40);
            label1.Name = "label1";
            label1.Size = new Size(216, 37);
            label1.TabIndex = 0;
            label1.Text = "Sorting Finished!";
            // 
            // lblTotal
            // 
            lblTotal.AutoSize = true;
            lblTotal.Font = new Font("Segoe UI", 15F);
            lblTotal.Location = new Point(279, 130);
            lblTotal.Name = "lblTotal";
            lblTotal.Size = new Size(113, 28);
            lblTotal.TabIndex = 1;
            lblTotal.Text = "Total files: 0";
            // 
            // lblPhotos
            // 
            lblPhotos.AutoSize = true;
            lblPhotos.Font = new Font("Segoe UI", 15F);
            lblPhotos.Location = new Point(279, 180);
            lblPhotos.Name = "lblPhotos";
            lblPhotos.Size = new Size(93, 28);
            lblPhotos.TabIndex = 2;
            lblPhotos.Text = "Photos: 0";
            // 
            // lblVideos
            // 
            lblVideos.AutoSize = true;
            lblVideos.Font = new Font("Segoe UI", 15F);
            lblVideos.Location = new Point(279, 230);
            lblVideos.Name = "lblVideos";
            lblVideos.Size = new Size(91, 28);
            lblVideos.TabIndex = 3;
            lblVideos.Text = "Videos: 0";
            // 
            // lblOcr
            // 
            lblOcr.AutoSize = true;
            lblOcr.Font = new Font("Segoe UI", 15F);
            lblOcr.Location = new Point(279, 280);
            lblOcr.Name = "lblOcr";
            lblOcr.Size = new Size(162, 28);
            lblOcr.TabIndex = 4;
            lblOcr.Text = "OCR processed: 0";
            // 
            // lblTime
            // 
            lblTime.AutoSize = true;
            lblTime.Font = new Font("Segoe UI", 15F);
            lblTime.Location = new Point(279, 330);
            lblTime.Name = "lblTime";
            lblTime.Size = new Size(206, 28);
            lblTime.TabIndex = 5;
            lblTime.Text = "Elapsed time: 00:00:00";
            // lblDevice
            lblDevice = new Label();
            lblDevice.AutoSize = true;
            lblDevice.Font = new Font("Segoe UI", 15F);
            lblDevice.Location = new Point(279, 380);
            lblDevice.Name = "lblDevice";
            lblDevice.Size = new Size(200, 28);
            lblDevice.TabIndex = 6;
            lblDevice.Text = "OCR device used: ?";
            // 
            // SummaryForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblTime);
            Controls.Add(lblOcr);
            Controls.Add(lblVideos);
            Controls.Add(lblPhotos);
            Controls.Add(lblTotal);
            Controls.Add(label1);
            Controls.Add(lblDevice);
            Name = "SummaryForm";
            Text = "SortMyMedia 2.0 – Summary";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label lblTotal;
        private Label lblPhotos;
        private Label lblVideos;
        private Label lblOcr;
        private Label lblTime;
        private Label lblDevice;
    }
}