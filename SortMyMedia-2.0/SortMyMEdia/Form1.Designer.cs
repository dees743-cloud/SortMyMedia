namespace SortMyMedia
{
    partial class Form1
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            label1 = new Label();
            textBox1 = new TextBox();
            button1 = new Button();
            label2 = new Label();
            textBox2 = new TextBox();
            button2 = new Button();
            button3 = new Button();
            progressBar1 = new ProgressBar();
            labelFileProgress = new Label();
            progressBarOcr = new ProgressBar();
            labelOcr = new Label();
            label3 = new Label();
            listBox1 = new ListBox();
            labelSortMode = new Label();
            comboSortMode = new ComboBox();
            lblTimer = new Label();
            checkBoxEnableOcr = new CheckBox();
            checkBoxHtmlGenerator = new CheckBox();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(75, 27);
            label1.Name = "label1";
            label1.Size = new Size(74, 15);
            label1.TabIndex = 0;
            label1.Text = "Input Folder:";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(155, 19);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(552, 23);
            textBox1.TabIndex = 1;
            // 
            // button1
            // 
            button1.Location = new Point(713, 19);
            button1.Name = "button1";
            button1.Size = new Size(75, 23);
            button1.TabIndex = 2;
            button1.Text = "Browse";
            button1.Click += button1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(75, 55);
            label2.Name = "label2";
            label2.Size = new Size(81, 15);
            label2.TabIndex = 3;
            label2.Text = "Output Folder";
            // 
            // textBox2
            // 
            textBox2.Location = new Point(155, 47);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(552, 23);
            textBox2.TabIndex = 4;
            // 
            // button2
            // 
            button2.Location = new Point(713, 47);
            button2.Name = "button2";
            button2.Size = new Size(75, 23);
            button2.TabIndex = 5;
            button2.Text = "Browse";
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new Point(261, 120);
            button3.Name = "button3";
            button3.Size = new Size(92, 23);
            button3.TabIndex = 9;
            button3.Text = "Start Sorting";
            button3.Click += button3_Click;
            // 
            // buttonExit
            // 
            buttonExit = new Button();
            buttonExit.Location = new Point(359, 120);
            buttonExit.Name = "buttonExit";
            buttonExit.Size = new Size(75, 23);
            buttonExit.TabIndex = 18;
            buttonExit.Text = "Exit";
            buttonExit.Click += (s, e) => Application.Exit();
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(261, 159);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(446, 23);
            progressBar1.TabIndex = 10;
            // 
            // labelFileProgress
            // 
            labelFileProgress.AutoSize = true;
            labelFileProgress.Location = new Point(175, 159);
            labelFileProgress.Name = "labelFileProgress";
            labelFileProgress.Size = new Size(76, 15);
            labelFileProgress.TabIndex = 11;
            labelFileProgress.Text = "File progress";
            // 
            // progressBarOcr
            // 
            progressBarOcr.Location = new Point(261, 206);
            progressBarOcr.Name = "progressBarOcr";
            progressBarOcr.Size = new Size(446, 23);
            progressBarOcr.TabIndex = 12;
            progressBarOcr.Visible = false;
            // 
            // labelOcr
            // 
            labelOcr.AutoSize = true;
            labelOcr.Location = new Point(155, 206);
            labelOcr.Name = "labelOcr";
            labelOcr.Size = new Size(82, 15);
            labelOcr.TabIndex = 13;
            labelOcr.Text = "OCR progress";
            labelOcr.Visible = false;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(261, 260);
            label3.Name = "label3";
            label3.Size = new Size(27, 15);
            label3.TabIndex = 15;
            label3.Text = "log:";
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.Location = new Point(12, 280);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(776, 154);
            listBox1.TabIndex = 16;
            listBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // labelSortMode
            // 
            labelSortMode.AutoSize = true;
            labelSortMode.Location = new Point(75, 85);
            labelSortMode.Name = "labelSortMode";
            labelSortMode.Size = new Size(91, 15);
            labelSortMode.TabIndex = 6;
            labelSortMode.Text = "Folder Structure";
            // 
            // comboSortMode
            // 
            comboSortMode.DropDownStyle = ComboBoxStyle.DropDownList;
            comboSortMode.Items.AddRange(new object[] { "Daily", "Montly" });
            comboSortMode.Location = new Point(172, 77);
            comboSortMode.Name = "comboSortMode";
            comboSortMode.Size = new Size(150, 23);
            comboSortMode.TabIndex = 7;
            // 
            // lblTimer
            // 
            lblTimer.AutoSize = true;
            lblTimer.Location = new Point(693, 260);
            lblTimer.Name = "lblTimer";
            lblTimer.Size = new Size(95, 15);
            lblTimer.TabIndex = 17;
            lblTimer.Text = "Elapsed: 00:00:00";
            lblTimer.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            // 
            // checkBoxEnableOcr
            // 
            checkBoxEnableOcr.AutoSize = true;
            checkBoxEnableOcr.Location = new Point(340, 79);
            checkBoxEnableOcr.Name = "checkBoxEnableOcr";
            checkBoxEnableOcr.Size = new Size(88, 19);
            checkBoxEnableOcr.TabIndex = 8;
            checkBoxEnableOcr.Text = "Enable OCR";
            // 
            // checkBoxHtmlGenerator
            // 
            checkBoxHtmlGenerator.AutoSize = true;
            checkBoxHtmlGenerator.Location = new Point(172, 79);
            checkBoxHtmlGenerator.Name = "checkBoxHtmlGenerator";
            checkBoxHtmlGenerator.Size = new Size(113, 19);
            checkBoxHtmlGenerator.TabIndex = 19;
            checkBoxHtmlGenerator.Text = "HTML generator";
            checkBoxHtmlGenerator.Visible = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(label1);
            Controls.Add(textBox1);
            Controls.Add(button1);
            Controls.Add(label2);
            Controls.Add(textBox2);
            Controls.Add(button2);
            Controls.Add(labelSortMode);
            Controls.Add(comboSortMode);
            Controls.Add(checkBoxEnableOcr);
            Controls.Add(checkBoxHtmlGenerator);
            Controls.Add(button3);
            Controls.Add(buttonExit);
            Controls.Add(progressBar1);
            Controls.Add(labelFileProgress);
            Controls.Add(progressBarOcr);
            Controls.Add(labelOcr);
            Controls.Add(label3);
            Controls.Add(listBox1);
            Controls.Add(lblTimer);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "SortMyMedia";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox textBox1;
        private Button button1;
        private Label label2;
        private TextBox textBox2;
        private Button button2;
        private Button button3;
        private Button buttonExit;
        private ProgressBar progressBar1;
        private Label labelFileProgress;
        private ProgressBar progressBarOcr;
        private Label labelOcr;
        private Label label3;
        private ListBox listBox1;
        private Label labelSortMode;
        private ComboBox comboSortMode;
        private Label lblTimer;
        private CheckBox checkBoxEnableOcr;
        private CheckBox checkBoxHtmlGenerator;
    }
}