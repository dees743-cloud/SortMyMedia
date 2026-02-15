using System;
using System.Windows.Forms;

namespace SortMyMedia
{
    public partial class SummaryForm : Form
    {
        public SummaryForm(int total, int photos, int videos, string time)
        {
            InitializeComponent();

            lblTotal.Text = "Total files: " + total;
            lblPhotos.Text = "Photos: " + photos;
            lblVideos.Text = "Videos: " + videos;
            lblTime.Text = "Elapsed time: " + time;
        }
    }
}