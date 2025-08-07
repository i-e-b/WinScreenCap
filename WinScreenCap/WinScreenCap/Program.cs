using System;
using System.Windows.Forms;
using Gma.UserActivityMonitor;

namespace WinScreenCap
{
    static class Program
    {
        public static volatile bool MouseDown = false;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            CaptureMouseEvents();

            Application.Run(new ScreenCaptureForm());
        }

        private static void CaptureMouseEvents()
        {
            HookManager.MouseUp += OnMouseUp;
            HookManager.MouseDown += OnMouseDown;
        }

        private static void OnMouseDown(object? sender, MouseEventArgs e)
        {
            MouseDown = true;
        }

        private static void OnMouseUp(object? sender, MouseEventArgs e)
        {
            MouseDown = false;
        }
    }
}