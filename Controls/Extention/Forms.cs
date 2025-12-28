using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public static class Forms
    {
        // ──────────────────────────────────────────────────────────────
        // Safe WM_SETREDRAW with reference counting (prevents unbalanced calls)
        // ──────────────────────────────────────────────────────────────
        private static class DrawingSuspension
        {
            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            private const int WM_SETREDRAW = 0x000B;

            private static readonly Dictionary<IntPtr, int> _suspendCounts = new();

            public static void Suspend(Control control)
            {
                if (control == null || !control.IsHandleCreated) return;
                var handle = control.Handle;

                lock (_suspendCounts)
                {
                    _suspendCounts.TryGetValue(handle, out int count);
                    count++;
                    _suspendCounts[handle] = count;

                    if (count == 1)
                        SendMessage(handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
                }
            }

            public static void Resume(Control control)
            {
                if (control == null || !control.IsHandleCreated) return;
                var handle = control.Handle;

                lock (_suspendCounts)
                {
                    if (!_suspendCounts.TryGetValue(handle, out int count) || count <= 0)
                        return;

                    count--;
                    _suspendCounts[handle] = count;

                    if (count == 0)
                    {
                        SendMessage(handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                        control.Invalidate(true);
                        control.Update();
                        _suspendCounts.Remove(handle);
                    }
                }
            }
        }

        // ──────────────────────────────────────────────────────────────
        // Replaces content in container with new UserControl (main method)
        // ──────────────────────────────────────────────────────────────
        public static void LoadUserControl(this UserControl control, Panel container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (control == null) throw new ArgumentNullException(nameof(control));

            container.EnabledBuffer();

            DrawingSuspension.Suspend(container);
            container.SuspendLayout();

            try
            {
                // Clean up previous UserControls safely
                for (int i = container.Controls.Count - 1; i >= 0; i--)
                {
                    if (container.Controls[i] is UserControl uc)
                    {
                        container.Controls.RemoveAt(i);
                        uc.Dispose();
                    }
                }

                control.Dock = DockStyle.Fill;
                container.Controls.Add(control);

                // One-time post-creation setup
                control.HandleCreated += OnUserControlHandleCreated;
            }
            finally
            {
                container.ResumeLayout(true);
                DrawingSuspension.Resume(container);
            }
        }

        private static void OnUserControlHandleCreated(object sender, EventArgs e)
        {
            if (sender is not UserControl uc) return;

            if (uc.CanFocus) uc.Focus();
            if (uc.AutoScroll && uc.ActiveControl != null)
                uc.ScrollControlIntoView(uc.ActiveControl);

            if (HasDataGridView(uc))
                uc.ClearGridSelectionOnce();
        }

        // ──────────────────────────────────────────────────────────────
        // Dimmed background for modal-like dialogs (no leak)
        // ──────────────────────────────────────────────────────────────
        public static void ShowWithDimmedBackground(this Form dialog, Form owner = null)
        {
            if (owner == null)
            {
                dialog.ShowDialog();
                return;
            }

            using var overlay = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.Black,
                Opacity = 0.45,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Bounds = Screen.FromControl(owner).WorkingArea,
                TopMost = true
            };

            overlay.Show(owner);
            dialog.StartPosition = FormStartPosition.CenterParent;
            dialog.ShowDialog(owner);
            // overlay auto-disposes via using block
        }

        // ──────────────────────────────────────────────────────────────
        // Per-instance sidebar state (no static bool)
        // ──────────────────────────────────────────────────────────────
        public class SidebarState
        {
            public bool IsOpen { get; set; } = true;
        }

        public static async Task ToggleSidebarAsync(
            this Panel sidebar,
            Button[] toggleButtons,
            SidebarState state = null,
            CancellationToken cancellationToken = default)
        {
            state ??= sidebar.Tag as SidebarState ?? (sidebar.Tag = new SidebarState()) as SidebarState;

            state.IsOpen = !state.IsOpen;
            int targetWidth = state.IsOpen ? 200 : 80;

            await AnimateWidthAsync(sidebar, targetWidth, stepDelayMs: 8, cancellationToken);

            UpdateButtonStyle(toggleButtons, !state.IsOpen);
        }

        private static async Task AnimateWidthAsync(
            Panel panel,
            int targetWidth,
            int stepDelayMs = 8,
            CancellationToken ct = default)
        {
            if (panel.IsDisposed) return;

            int startWidth = panel.Width;
            int diff = targetWidth - startWidth;
            int steps = Math.Abs(diff) / 10; // ~10px per step
            steps = Math.Max(1, steps);

            for (int i = 0; i <= steps; i++)
            {
                ct.ThrowIfCancellationRequested();

                double t = (double)i / steps;
                double eased = EaseInOutCubic(t);

                panel.Width = startWidth + (int)(diff * eased);

                await Task.Delay(stepDelayMs, ct);
            }

            if (!panel.IsDisposed)
                panel.Width = targetWidth;
        }

        private static double EaseInOutCubic(double t)
        {
            return t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }

        public static void UpdateButtonStyle(Button[] buttons, bool showIconOnly)
        {
            if (buttons == null || buttons.Length == 0) return;

            foreach (var btn in buttons)
            {
                if (btn?.Tag == null) continue;

                btn.Text = showIconOnly ? "" : btn.Tag.ToString();
                btn.Refresh();
            }
        }

        // ──────────────────────────────────────────────────────────────
        // DataGridView selection clearing – handlers added ONLY ONCE
        // ──────────────────────────────────────────────────────────────
        private static readonly object _dgvLock = new();
        private static readonly HashSet<DataGridView> _selectionHandlers = new();

        public static void ClearGridSelectionOnce(this UserControl userControl)
        {
            foreach (var dgv in FindAllDataGridViews(userControl))
            {
                dgv.ClearSelection();

                lock (_dgvLock)
                {
                    if (_selectionHandlers.Add(dgv))
                    {
                        dgv.CellLeave += (s, e) => dgv.ClearSelection();
                    }
                }
            }
        }

        private static IEnumerable<DataGridView> FindAllDataGridViews(Control root)
        {
            if (root == null) yield break;

            var stack = new Stack<Control>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current is DataGridView dgv)
                    yield return dgv;

                if (!current.HasChildren) continue;

                for (int i = current.Controls.Count - 1; i >= 0; i--)
                    stack.Push(current.Controls[i]);
            }
        }

        public static bool HasDataGridView(this Control control) =>
            FindAllDataGridViews(control).GetEnumerator().MoveNext();

        // ──────────────────────────────────────────────────────────────
        // Optional: Generic form helper
        // ──────────────────────────────────────────────────────────────
        public static void ControlAction<T>(
            Control caller,
            Action<T> configure,
            bool show = false,
            bool modal = false)
            where T : Form, new()
        {
            Form parent = caller?.FindForm();

            if (parent is T existing)
            {
                configure?.Invoke(existing);
                return;
            }

            using var form = new T();
            configure?.Invoke(form);

            if (show)
            {
                if (modal)
                    form.ShowDialog(parent);
                else
                    form.Show(parent);
            }
        }
    
        /// <summary>
        /// Centers a child form perfectly on its parent form, even if resized or on multi-monitor setups.
        /// </summary>
        /// <param name="child">The form to center (this)</param>
        /// <example>
        /// <code>
        /// var dialog = new SettingsForm();
        /// dialog.Owner = this;
        /// dialog.CenterToParent();
        /// dialog.ShowDialog();
        /// </code>
        /// </example>
        public static void CenterToParent(this Form child)
        {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (child.Owner == null) return;

            child.StartPosition = FormStartPosition.Manual;
            child.Location = new Point(
                child.Owner.Left + (child.Owner.Width - child.Width) / 2,
                child.Owner.Top + (child.Owner.Height - child.Height) / 2);

            // Ensure it doesn't go off-screen
            var screen = Screen.FromPoint(child.Location);
            child.Location = new Point(
                Math.Max(screen.WorkingArea.Left, Math.Min(child.Location.X, screen.WorkingArea.Right - child.Width)),
                Math.Max(screen.WorkingArea.Top, Math.Min(child.Location.Y, screen.WorkingArea.Bottom - child.Height))
            );
        }

        /// <summary>
        /// Smooth fade-in animation when form appears (like modern apps).
        /// </summary>
        /// <param name="form">The form to fade in</param>
        /// <param name="interval">Timer interval in ms (lower = smoother, default 20)</param>
        /// <example>
        /// <code>
        /// public LoginForm()
        /// {
        ///     InitializeComponent();
        ///     this.Opacity = 0;
        ///     this.Load += (s, e) => this.FadeIn();
        /// }
        /// </code>
        /// </example>
        public static void FadeIn(this Form form, int interval = 20)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            var timer = new System.Windows.Forms.Timer { Interval = interval };
            timer.Tick += (s, e) =>
            {
                if (form.Opacity >= 1.0)
                {
                    form.Opacity = 1.0;
                    timer.Stop();
                    timer.Dispose();
                }
                else
                {
                    form.Opacity += 0.06;
                }
            };

            form.Opacity = 0;
            timer.Start();
        }

        /// <summary>
        /// Smooth fade-out animation with optional callback and auto-close.
        /// </summary>
        /// <param name="form">The form to fade out</param>
        /// <param name="onComplete">Action to run after fade (e.g. Application.Exit())</param>
        /// <param name="interval">Timer interval in ms (default 20)</param>
        /// <example>
        /// <code>
        /// btnExit.Click += (s, e) => this.FadeOut(() => Application.Exit());
        /// </code>
        /// </example>
        public static void FadeOut(this Form form, Action onComplete = null, int interval = 20)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            var timer = new System.Windows.Forms.Timer { Interval = interval };
            timer.Tick += (s, e) =>
            {
                if (form.Opacity <= 0.0)
                {
                    timer.Stop();
                    timer.Dispose();
                    onComplete?.Invoke();
                    form.Close();
                }
                else
                {
                    form.Opacity -= 0.06;
                }
            };

            timer.Start();
        }

        /// <summary>
        /// Flash the form in the taskbar when minimized (like Messenger, Discord, or notifications).
        /// </summary>
        /// <param name="form">The form to flash</param>
        /// <param name="count">Number of flashes (default 5)</param>
        /// <example>
        /// <code>
        /// // In background worker when new message arrives
        /// if (this.WindowState == FormWindowState.Minimized)
        ///     this.FlashWindow(6);
        /// </code>
        /// </example>
        public static void FlashWindow(this Form form, uint count = 5)
        {
            if (form == null) throw new ArgumentNullException(nameof(form));

            var flashInfo = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = form.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG, // Flash both caption and taskbar button
                uCount = count,
                dwTimeout = 0 // Use system default blink rate
            };

            FlashWindowEx(ref flashInfo);
        }

        // P/Invoke structures and methods
        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 0x00000003;
        private const uint FLASHW_TIMERNOFG = 0x0000000C;

        [DllImport("user32.dll")]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
    }
}

