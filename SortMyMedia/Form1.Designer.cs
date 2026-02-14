namespace SortMyMedia
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
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
            label3 = new Label();
            listBox1 = new ListBox();
            labelSortMode = new Label();
            comboSortMode = new ComboBox();
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
            button1.UseVisualStyleBackColor = true;
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
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // button3
            // 
            button3.Location = new Point(261, 120);
            button3.Name = "button3";
            button3.Size = new Size(92, 23);
            button3.TabIndex = 6;
            button3.Text = "Start Sorting";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // progressBar1
            // 
            progressBar1.Location = new Point(261, 159);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(446, 23);
            progressBar1.TabIndex = 7;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(261, 195);
            label3.Name = "label3";
            label3.Size = new Size(27, 15);
            label3.TabIndex = 8;
            label3.Text = "log:";
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.Location = new Point(12, 213);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(776, 214);
            listBox1.TabIndex = 9;
            // 
            // labelSortMode
            // 
            labelSortMode.AutoSize = true;
            labelSortMode.Location = new Point(75, 85);
            labelSortMode.Name = "labelSortMode";
            labelSortMode.Size = new Size(71, 15);
            labelSortMode.TabIndex = 10;
            labelSortMode.Text = "Sorteren op:";
            // 
            // comboSortMode
            // 
            comboSortMode.DropDownStyle = ComboBoxStyle.DropDownList;
            comboSortMode.FormattingEnabled = true;
            comboSortMode.Items.AddRange(new object[] { "Per dag", "Per maand" });
            comboSortMode.Location = new Point(155, 82);
            comboSortMode.Name = "comboSortMode";
            comboSortMode.Size = new Size(150, 23);
            comboSortMode.TabIndex = 11;
            comboSortMode.SelectedIndexChanged += comboSortMode_SelectedIndexChanged;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(comboSortMode);
            Controls.Add(labelSortMode);
            Controls.Add(listBox1);
            Controls.Add(label3);
            Controls.Add(progressBar1);
            Controls.Add(button3);
            Controls.Add(button2);
            Controls.Add(textBox2);
            Controls.Add(label2);
            Controls.Add(button1);
            Controls.Add(textBox1);
            Controls.Add(label1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Name = "Form1";
            Text = "SortMyMedia";
            Load += Form1_Load;
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
        private ProgressBar progressBar1;
        private Label label3;
        private ListBox listBox1;
        private Label labelSortMode;
        private ComboBox comboSortMode;
    }
}