using System.Drawing;
using System.Windows.Forms;

namespace Next.Controls
{
    public static class Toast
    {
        public static void Show(string message, int timeout = 3000, Form owner = null)
        {
            var toast = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(300, 80),
                BackColor = Color.FromArgb(50, 50, 50),
                Opacity = 0.95,
                TopMost = true,
                ShowInTaskbar = false
            };

            var label = new Label
            {
                Text = message,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            toast.Controls.Add(label);

            // Position bottom-right
            var workingArea = Screen.PrimaryScreen.WorkingArea;
            toast.Location = new Point(workingArea.Width - toast.Width - 20, workingArea.Height - toast.Height - 20);

            toast.Show();
            Timer t = new Timer { Interval = timeout };
            t.Tick += (s, e) => { toast.Close(); t.Dispose(); };
            t.Start();
        }
    }
}
