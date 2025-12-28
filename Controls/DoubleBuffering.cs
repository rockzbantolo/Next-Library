using System.Reflection;
using System.Windows.Forms;

namespace Next.Controls
{
    public static class DoubleBuffering
    {
        public static void EnabledBuffer(this Control control)
        {
            if (control == null) return;

            var prop = control.GetType().GetProperty("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
                prop.SetValue(control, true, null);
        }
    }
}
