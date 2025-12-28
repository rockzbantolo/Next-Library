using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Next.Controls
{
    ///<summary>
    /// Add this to InitializedComponents - RadiusProvider = new RadiusProvider();
    /// Call this in the Class - private RadiusProvider RadiusProvider;
    ///</summary>
    [ToolboxItem(true)]
    [ProvideProperty("Radius", typeof(Control))]
    [ProvideProperty("BorderColor", typeof(Control))]
    [ProvideProperty("BorderWidth", typeof(Control))]
    public sealed class RadiusProvider : Component, IExtenderProvider
    {
        private sealed class RadiusProps
        {
            public int Radius;
            public Color BorderColor = Color.Black;
            public int BorderWidth = 1;
            public GraphicsPath Path;
            public Rectangle LastBounds;
        }

        private readonly ConditionalWeakTable<Control, RadiusProps> _map = new();

        // Debounce support for designer performance
        private Timer _updateTimer;
        private Control _pendingControl;
        private RadiusProps _pendingProps;

        public bool CanExtend(object extendee) => extendee is Control;

        // ── Radius ──────────────────────────────────────────────────────────────
        [Category("Appearance")]
        [DisplayName("Border Radius")]
        [Description("Corner radius of the control")]
        [DefaultValue(0)]
        public int GetRadius(Control c) => Get(c).Radius;

        public void SetRadius(Control c, int value)
        {
            var p = Get(c);
            value = Math.Max(0, value);
            if (p.Radius == value) return;

            p.Radius = value;
            Hook(c);

            // Debounce expensive update in designer / rapid changes
            ScheduleUpdate(c, p);
        }

        // ── Border Color ────────────────────────────────────────────────────────
        [Category("Appearance")]
        [DefaultValue(typeof(Color), "Black")]
        [DisplayName("Border Color")]
        [Description("Border color of the control")]
        public Color GetBorderColor(Control c) => Get(c).BorderColor;

        public void SetBorderColor(Control c, Color value)
        {
            var p = Get(c);
            if (p.BorderColor == value) return;

            p.BorderColor = value;
            c.Invalidate();
        }

        // ── Border Width ────────────────────────────────────────────────────────
        [Category("Appearance")]
        [DefaultValue(1)]
        [DisplayName("Border Width")]
        [Description("Border thickness of the control")]
        public int GetBorderWidth(Control c) => Get(c).BorderWidth;

        public void SetBorderWidth(Control c, int value)
        {
            var p = Get(c);
            value = Math.Max(0, value);
            if (p.BorderWidth == value) return;

            p.BorderWidth = value;
            ScheduleUpdate(c, p);
        }

        // ── Core ────────────────────────────────────────────────────────────────
        private RadiusProps Get(Control c) =>
            _map.GetValue(c, _ => new RadiusProps());

        private void Hook(Control c)
        {
            c.Paint -= OnPaint;
            c.Resize -= OnResize;
            c.Disposed -= OnDisposed;

            c.Paint += OnPaint;
            c.Resize += OnResize;
            c.Disposed += OnDisposed;

            EnableDoubleBuffer(c);
        }

        private void OnDisposed(object sender, EventArgs e)
        {
            if (sender is not Control c) return;
            if (!_map.TryGetValue(c, out var p)) return;

            p.Path?.Dispose();
            c.Region?.Dispose();
            _map.Remove(c);

            // Clean up pending update if control is being disposed
            if (_pendingControl == c)
            {
                _pendingControl = null;
                _pendingProps = null;
            }
        }

        private void OnResize(object sender, EventArgs e)
        {
            if (sender is not Control c) return;
            if (!_map.TryGetValue(c, out var p)) return;

            UpdateRegionAndPath(c, p);
            c.Invalidate();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (sender is not Control c) return;
            if (!_map.TryGetValue(c, out var p)) return;
            if (p.Path == null || p.BorderWidth <= 0) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using var pen = new Pen(p.BorderColor, p.BorderWidth)
            {
                Alignment = PenAlignment.Inset
            };

            g.DrawPath(pen, p.Path);
        }

        // ── Debounced Update ────────────────────────────────────────────────────
        private void ScheduleUpdate(Control c, RadiusProps p)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                // In designer → debounce
                if (_updateTimer == null)
                {
                    _updateTimer = new Timer();
                    _updateTimer.Interval = 180; // 180 ms – feels responsive but reduces spam
                    _updateTimer.Tick += Timer_Tick;
                }

                _pendingControl = c;
                _pendingProps = p;

                _updateTimer.Stop();
                _updateTimer.Start();
            }
            else
            {
                // Runtime → apply immediately
                UpdateRegionAndPath(c, p);
                c.Invalidate();
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            _updateTimer?.Stop();

            if (_pendingControl != null && _pendingProps != null)
            {
                UpdateRegionAndPath(_pendingControl, _pendingProps);
                _pendingControl.Invalidate();
            }

            _pendingControl = null;
            _pendingProps = null;
        }

        private void UpdateRegionAndPath(Control c, RadiusProps p)
        {
            var currentRect = c.ClientRectangle;
            if (currentRect == p.LastBounds && p.Path != null)
                return;

            p.Path?.Dispose();
            p.Path = CreateRoundedPath(currentRect, p.Radius, p.BorderWidth);
            p.LastBounds = currentRect;

            if (c.Visible)
            {
                c.Region?.Dispose();
                c.Region = new Region(p.Path);
            }
        }

        // ── Path Creation ───────────────────────────────────────────────────────
        private static GraphicsPath CreateRoundedPath(Rectangle r, int radius, int border)
        {
            var path = new GraphicsPath();

            if (r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }

            var drawRect = r;
            if (border > 0)
                drawRect.Inflate(-border / 2, -border / 2);

            radius = Math.Min(radius, Math.Min(drawRect.Width, drawRect.Height) / 2);

            if (radius <= 0)
            {
                path.AddRectangle(drawRect);
                return path;
            }

            int diameter = radius * 2;

            path.AddArc(drawRect.Left, drawRect.Top, diameter, diameter, 180, 90);
            path.AddArc(drawRect.Right - diameter, drawRect.Top, diameter, diameter, 270, 90);
            path.AddArc(drawRect.Right - diameter, drawRect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(drawRect.Left, drawRect.Bottom - diameter, diameter, diameter, 90, 90);

            path.CloseFigure();
            return path;
        }

        // ── Double Buffering ────────────────────────────────────────────────────
        private static void EnableDoubleBuffer(Control c)
        {
            var pi = c.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            pi?.SetValue(c, true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Dispose();
                _updateTimer = null;
            }
            base.Dispose(disposing);
        }
    }
}