using System;
using System.Windows.Forms;

namespace SortMyMedia
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

            try
            {
                Application.Run(new AppContext());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Fatal startup error: {ex.Message}",
                    "SortMyMedia 2.0",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
}