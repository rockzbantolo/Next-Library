using System.Drawing;
using System.Windows.Forms;

namespace Next.Common
{
    public static class Notifications
    {
        private static readonly NotifyIcon notifyIcon;

        static Notifications()
        {
            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true
            };
        }

        public static void Show(string title, string message, ToolTipIcon iconType = ToolTipIcon.Info, int durationMs = 3000)
        {
            if (notifyIcon == null) return;

            // Classic switch for VS2017 compatibility
            switch (iconType)
            {
                case ToolTipIcon.Error:
                    notifyIcon.Icon = SystemIcons.Error;
                    break;
                case ToolTipIcon.Warning:
                    notifyIcon.Icon = SystemIcons.Warning;
                    break;
                case ToolTipIcon.Info:
                    notifyIcon.Icon = SystemIcons.Information;
                    break;
                default:
                    notifyIcon.Icon = SystemIcons.Application;
                    break;
            }

            notifyIcon.ShowBalloonTip(durationMs, title, message, iconType);
        }

        public static void Dispose()
        {
            notifyIcon.Dispose();
        }
    }
}
