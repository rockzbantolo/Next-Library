using System;
using System.Windows.Forms;

namespace Next.Controls
{
    public static class Taskbars
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool FlashWindow(IntPtr hwnd, bool bInvert);

        // Flash taskbar icon to get attention
        public static void FlashTaskbar(this Form form, int count = 5)
        {
            for (int i = 0; i < count * 2; i++)
            {
                FlashWindow(form.Handle, true);
                System.Threading.Thread.Sleep(500);
            }
        }
    }
}
