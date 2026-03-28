using System;
using System.Windows.Forms;

namespace SortMyMedia
{
    public class AppContext : ApplicationContext
    {
        private SplashScreen splash;
        private Form1 main = null!;

        public AppContext()
        {
            splash = new SplashScreen();
            splash.Show();

            // Start fade-in automatisch
            splash.Shown += (s, e) =>
            {
                var timer = new System.Windows.Forms.Timer();
                timer.Interval = 1500;
                timer.Tick += (s2, e2) =>
                {
                    timer.Stop();
                    splash.BeginFadeOut();
                };
                timer.Start();
            };

            splash.FormClosed += SplashClosed;
        }

        private void SplashClosed(object? sender, EventArgs e)
        {
            splash.FormClosed -= SplashClosed;

            main = new Form1();
            main.FormClosed += (s, e2) => ExitThread();
            main.Show();
        }
    }
}