using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public static class Buttons
    {
        // Button Loader
        private const string Key = "__BTN_LOADING__";

        #region Button Loader

            public static void SetLoading(this Button btn, bool loading)
            {
                if (btn == null) return;

                // Clean up old handlers
                btn.Paint -= Button_Paint;

                if (!loading)
                {
                    // Restore original text
                    if (btn.Tag is string tag && tag.Contains("|"))
                    {
                        btn.Text = tag.Split('|')[0];
                    }
                    btn.Tag = null;
                    btn.UseVisualStyleBackColor = true;
                    btn.Invalidate();
                    return;
                }

                // Save original text
                if (btn.Tag == null || !btn.Tag.ToString().Contains(Key))
                {
                    btn.Tag = btn.Text + "|" + Key;
                }

                btn.Text = "";  // clean modern look
                btn.UseVisualStyleBackColor = false;

                btn.Paint += Button_Paint;
                StartButtonTimer(btn);
                btn.Invalidate();
            }

            private static void StartButtonTimer(Button btn)
            {
                Timer timer = new Timer();
                timer.Interval = 16; // ~60 FPS

                timer.Tick += (s, e) =>
                {
                    if (btn.IsDisposed || btn.Tag == null || !btn.Tag.ToString().Contains(Key))
                    {
                        timer.Stop();
                        timer.Dispose();
                        return;
                    }
                    btn.Invalidate(); // only this button repaints
                };

                timer.Start();

                // Auto cleanup
                btn.Disposed += (s, e) => { timer.Stop(); timer.Dispose(); };
            }

            private static void Button_Paint(object sender, PaintEventArgs e)
            {
                System.Windows.Forms.Button btn = (System.Windows.Forms.Button)sender;
                if (btn.Tag == null || !btn.Tag.ToString().Contains(Key)) return;

                Rectangle rect = btn.ClientRectangle;
                float size = Math.Min(rect.Width, rect.Height) * 0.4f;
                float thickness = Math.Max(2.5f, size * 0.15f);
                PointF center = new PointF(rect.Width / 2f, rect.Height / 2f);

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                float angle = (Environment.TickCount % 1200) / 1200f * 360f;

                Pen pen = new Pen(btn.Enabled ? btn.ForeColor : SystemColors.GrayText, thickness);
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;

                try
                {
                    for (int i = 0; i < 330; i += 10)
                    {
                        float a = angle + i;
                        float opacity = 1f - (i / 330f);
                        if (opacity < 0.1f) continue;

                        pen.Color = Color.FromArgb((int)(opacity * 255), btn.ForeColor);

                        float rad1 = a * (float)Math.PI / 180f;
                        float rad2 = (a + 10) * (float)Math.PI / 180f;

                        PointF p1 = new PointF(
                            center.X + (float)Math.Cos(rad1) * (size / 2),
                            center.Y + (float)Math.Sin(rad1) * (size / 2)
                        );
                        PointF p2 = new PointF(
                            center.X + (float)Math.Cos(rad2) * (size / 2),
                            center.Y + (float)Math.Sin(rad2) * (size / 2)
                        );

                        e.Graphics.DrawLine(pen, p1, p2);
                    }
                }
                finally
                {
                    pen.Dispose();
                }
            }

        #endregion

        #region Hover/Focus Color Effects

            public static void EnableHoverEffect(this Button button, Color hoverColor)
            {
                Color normalColor = button.BackColor;
                Color normalFore = button.ForeColor;

                button.MouseEnter += (s, e) => { button.BackColor = hoverColor; };
                button.MouseLeave += (s, e) => { button.BackColor = normalColor; };
                button.MouseDown += (s, e) => { button.ForeColor = Color.White; };
                button.MouseUp += (s, e) => { button.ForeColor = normalFore; };
            }

        #endregion
    }
}
