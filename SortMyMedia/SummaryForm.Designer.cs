namespace SortMyMedia
{
    partial class SummaryForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            lblTotal = new Label();
            lblPhotos = new Label();
            lblVideos = new Label();
            lblTime = new Label();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 20F);
            label1.ImageAlign = ContentAlignment.TopCenter;
            label1.Location = new Point(279, 40);
            label1.Name = "label1";
            label1.Size = new Size(216, 37);
            label1.TabIndex = 0;
            label1.Text = "Sorting Finished!";
            label1.TextAlign = ContentAlignment.TopCenter;
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
            lblPhotos.Location = new Point(279, 189);
            lblPhotos.Name = "lblPhotos";
            lblPhotos.Size = new Size(93, 28);
            lblPhotos.TabIndex = 2;
            lblPhotos.Text = "Photos: 0";
            // 
            // lblVideos
            // 
            lblVideos.AutoSize = true;
            lblVideos.Font = new Font("Segoe UI", 15F);
            lblVideos.Location = new Point(279, 245);
            lblVideos.Name = "lblVideos";
            lblVideos.Size = new Size(91, 28);
            lblVideos.TabIndex = 3;
            lblVideos.Text = "Videos: 0";
            // 
            // lblTime
            // 
            lblTime.AutoSize = true;
            lblTime.Font = new Font("Segoe UI", 15F);
            lblTime.Location = new Point(279, 307);
            lblTime.Name = "lblTime";
            lblTime.Size = new Size(206, 28);
            lblTime.TabIndex = 4;
            lblTime.Text = "Elapsed time: 00:00:00";
            // 
            // SummaryForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblTime);
            Controls.Add(lblVideos);
            Controls.Add(lblPhotos);
            Controls.Add(lblTotal);
            Controls.Add(label1);
            Name = "SummaryForm";
            Text = "SummaryForm";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private Label lblTotal;
        private Label lblPhotos;
        private Label lblVideos;
        private Label lblTime;
    }
}