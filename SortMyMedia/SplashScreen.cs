using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace SortMyMedia
{
    public partial class SplashScreen : Form
    {
        public SplashScreen()
        {
            InitializeComponent();
        }

        // ➜ HIER PLAATS JE DEZE METHODE
        public void BeginFadeOut()
        {
            timer2.Start();
        }


        private void SplashScreen_Load(object sender, EventArgs e)
        {
            this.Opacity = 0;      // begin volledig transparant
            timer1.Start();        // start fade-in
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (this.Opacity < 1)
                this.Opacity += 0.05;
            else
                timer1.Stop();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (this.Opacity > 0)
                this.Opacity -= 0.05;
            else
            {
                timer2.Stop();
                this.Close();
            }
        }
    }
}
