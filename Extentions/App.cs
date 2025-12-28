using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace Next.Extentions
{
    public static class App
    {
        // Restart the application
        public static void Restart(this Application app)
        {
            System.Diagnostics.Process.Start(Application.ExecutablePath);
            Application.Exit();
        }

        public static bool IsAlreadyRunning()
        {
            string proc = Process.GetCurrentProcess().ProcessName;
            return Process.GetProcessesByName(proc).Length > 1;
        }
    }
}
