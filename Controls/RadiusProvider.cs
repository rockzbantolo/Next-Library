using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Next.Controls
{
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
            public GraphicsPath CachedPath;
            public Rectangle LastBounds;
            public bool IsPainting;
        }

        private readonly ConditionalWeakTable<Control, RadiusProps> _map = new();

        // Debounce timer for designer
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
        private RadiusProps Get(Control c) => _map.GetValue(c, _ => new RadiusProps());

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

            p.CachedPath?.Dispose();
            c.Region?.Dispose();
            _map.Remove(c);

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
            if (p.IsPainting) return;

            UpdateRegionAndPath(c, p);
            c.Invalidate();
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            if (sender is not Control c) return;
            if (!_map.TryGetValue(c, out var p)) return;
            if (p.CachedPath == null || p.BorderWidth <= 0) return;

            p.IsPainting = true;
            try
            {
                var g = e.Graphics;
                // High-quality rendering for smoother borders & corners
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                using var pen = new Pen(p.BorderColor, p.BorderWidth)
                {
                    Alignment = PenAlignment.Inset
                };
                g.DrawPath(pen, p.CachedPath);
            }
            finally
            {
                p.IsPainting = false;
            }
        }

        // ── Debounced Update ────────────────────────────────────────────────────
        private void ScheduleUpdate(Control c, RadiusProps p)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                if (_updateTimer == null)
                {
                    _updateTimer = new Timer { Interval = 180 };
                    _updateTimer.Tick += Timer_Tick;
                }
                _pendingControl = c;
                _pendingProps = p;
                _updateTimer.Stop();
                _updateTimer.Start();
            }
            else
            {
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
            if (currentRect == p.LastBounds && p.CachedPath != null)
                return;

            p.CachedPath?.Dispose();
            p.CachedPath = CreateSmoothRoundedPath(currentRect, p.Radius, p.BorderWidth);
            p.LastBounds = currentRect;

            if (c.Visible)
            {
                c.Region?.Dispose();
                c.Region = new Region(p.CachedPath);
            }
        }

        // ── Smooth Bezier-based Rounded Path (with proper inset fix) ────────────
        private static GraphicsPath CreateSmoothRoundedPath(Rectangle bounds, int radius, int borderWidth)
        {
            var path = new GraphicsPath();

            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            // Full inset offset → Inset pen draws fully visible border (no right/bottom clipping)
            int offset = borderWidth;
            var inner = new Rectangle(
                bounds.X + offset,
                bounds.Y + offset,
                bounds.Width - 2 * offset,
                bounds.Height - 2 * offset
            );

            if (inner.Width <= 0 || inner.Height <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }

            radius = Math.Max(0, Math.Min(radius, Math.Min(inner.Width, inner.Height) / 2));

            if (radius <= 0)
            {
                path.AddRectangle(inner);
                return path;
            }

            int diameter = radius * 2;
            int left = inner.Left;
            int top = inner.Top;
            int right = inner.Right;
            int bottom = inner.Bottom;

            // Top-left
            path.AddBezier(left, top + radius,
                           left, top + radius / 2,
                           left + radius / 2, top,
                           left + radius, top);

            // Top
            path.AddLine(left + radius, top, right - radius, top);

            // Top-right
            path.AddBezier(right - radius, top,
                           right - radius / 2, top,
                           right, top + radius / 2,
                           right, top + radius);

            // Right
            path.AddLine(right, top + radius, right, bottom - radius);

            // Bottom-right
            path.AddBezier(right, bottom - radius,
                           right, bottom - radius / 2,
                           right - radius / 2, bottom,
                           right - radius, bottom);

            // Bottom
            path.AddLine(right - radius, bottom, left + radius, bottom);

            // Bottom-left
            path.AddBezier(left + radius, bottom,
                           left + radius / 2, bottom,
                           left, bottom - radius / 2,
                           left, bottom - radius);

            // Left (close)
            path.AddLine(left, bottom - radius, left, top + radius);

            path.CloseFigure();
            return path;
        }

        private static void EnableDoubleBuffer(Control c)
        {
            var pi = c.GetType().GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            pi?.SetValue(c, true);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _updateTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}