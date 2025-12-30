

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public static class Labels
    {

        #region Loader Animation - Skeleton / Spinner / Message

            private static readonly HashSet<Label> ActiveLabels = new();
            private static Timer _timer;
            private static float _shimmerOffset;

            private class LoaderState
            {
                public string PlaceholderText { get; set; }
                public Color BarColorFrom { get; set; }
                public Color BarColorTo { get; set; }
                public float ShimmerSpeed { get; set; }          
                public float Opacity { get; set; }
                public float ShimmerAngleDegrees { get; set; }     
                public int CornerRadius { get; set; }
                public float BarHeightRatio { get; set; }           
                public int Padding { get; set; } = 1;              
                public bool ShowPlaceholderText { get; set; } = true;
            }

            public static void SetLoading(
                this Label label,
                bool isLoading,
                string placeholderText = null,
                Color? barColorFrom = null,
                Color? barColorTo = null,
                float? shimmerSpeed = null,
                float? opacity = null,
                float? shimmerAngle = null,
                int? cornerRadius = null,
                float? barHeightRatio = null,
                int? padding = null,
                bool showPlaceholderText = true)
            {
                if (label == null || label.IsDisposed) return;

                // Clean up previous subscription
                label.Paint -= Label_PaintSkeleton;

                if (!isLoading)
                {
                    ActiveLabels.Remove(label);
                    label.Tag = null;
                    if (ActiveLabels.Count == 0) StopTimer();
                    label.Invalidate();
                    return;
                }

                // Enable double-buffering (reduces flicker)
                label.EnabledBuffer();

                var state = new LoaderState
                {
                    PlaceholderText = placeholderText,
                    BarColorFrom = barColorFrom ?? Color.LightGray,
                    BarColorTo = barColorTo ?? Color.WhiteSmoke,
                    ShimmerSpeed = shimmerSpeed ?? 5.5f,                                      // pixels per tick
                    Opacity = opacity ?? 0.95f,
                    ShimmerAngleDegrees = shimmerAngle ?? 15f,                                // gradient angle
                    CornerRadius = cornerRadius ?? 6,
                    BarHeightRatio = Math.Max(0.1f, Math.Min(barHeightRatio ?? 0.65f, 1.0f)), // 0.0 to 1.0 (1.0 = full height)
                    Padding = padding ?? 1,                                                   // space around the bar
                    ShowPlaceholderText = showPlaceholderText
                };

                label.Tag = state;
                ActiveLabels.Add(label);
                label.Paint += Label_PaintSkeleton;

                StartTimer();
                label.Invalidate();
            }

            private static void StartTimer()
            {
                if (_timer != null) return;

                _timer = new Timer { Interval = 40 }; // smoother with 40ms
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }

            private static void StopTimer()
            {
                if (_timer == null) return;
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer.Dispose();
                _timer = null;
            }

            private static void Timer_Tick(object sender, EventArgs e)
            {
                // You can make speed multiplier configurable too if needed
                _shimmerOffset += 5.5f;  // base movement — actual speed comes from LoaderState.ShimmerSpeed

                if (_shimmerOffset > 1200) _shimmerOffset -= 1200;

                foreach (var lbl in ActiveLabels.ToList())
                {
                    if (lbl.IsDisposed || !lbl.Visible || lbl.Tag is not LoaderState)
                    {
                        ActiveLabels.Remove(lbl);
                        continue;
                    }
                    lbl.Invalidate();
                }

                if (ActiveLabels.Count == 0)
                    StopTimer();
            }

            private static void Label_PaintSkeleton(object sender, PaintEventArgs e)
            {
                if (sender is not Label label || label.IsDisposed || label.Tag is not LoaderState state)
                    return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;

                var rect = label.ClientRectangle;
                if (rect.Width < 20 || rect.Height < 10) return;

                // Calculate bar dimensions based on ratio and padding
                int barHeight = (int)(rect.Height * state.BarHeightRatio);
                int verticalMargin = (rect.Height - barHeight) / 2;

                var barRect = new Rectangle(
                    state.Padding,
                    verticalMargin,
                    rect.Width - state.Padding * 2,
                    barHeight
                );

                // Shimmer gradient
                using var brush = new LinearGradientBrush(
                    barRect,
                    Color.FromArgb((int)(255 * state.Opacity), state.BarColorFrom),
                    Color.FromArgb((int)(255 * state.Opacity), state.BarColorTo),
                    state.ShimmerAngleDegrees);

                brush.TranslateTransform(_shimmerOffset * state.ShimmerSpeed / 5.5f, 0); // normalize to base speed

                using var path = GetRoundedRectPath(barRect, state.CornerRadius);
                g.FillPath(brush, path);

                // Optional placeholder text
                if (state.ShowPlaceholderText && !string.IsNullOrWhiteSpace(state.PlaceholderText))
                {
                    using var font = new Font("Segoe UI", 9f, FontStyle.Regular);
                    using var textBrush = new SolidBrush(Color.FromArgb(160, Color.DimGray));

                    var textSize = g.MeasureString(state.PlaceholderText, font);
                    float textX = state.Padding + 8;
                    float textY = verticalMargin + (barHeight - textSize.Height) / 2f;

                    // Keep text inside bar bounds
                    if (textX + textSize.Width + 8 < barRect.Right)
                    {
                        g.DrawString(state.PlaceholderText, font, textBrush, textX, textY);
                    }
                }
            }

            private static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
            {
                var path = new GraphicsPath();
                int diameter = radius * 2;

                var arc = new Rectangle(rect.X, rect.Y, diameter, diameter);
                path.AddArc(arc, 180, 90);

                arc.X = rect.Right - diameter;
                path.AddArc(arc, 270, 90);

                arc.Y = rect.Bottom - diameter;
                arc.X = rect.Right - diameter;
                path.AddArc(arc, 0, 90);

                arc.X = rect.X;
                path.AddArc(arc, 90, 90);

                path.CloseFigure();
                return path;
            }

        #endregion

        #region Data Bindings

            public enum NullDisplayMode
            {
                Empty,
                Dash,
                Zero,
                Custom
            }

            public sealed class LabelBinding
            {
                public Label Target { get; }
                public string ColumnName { get; }
                public NullDisplayMode? NullDisplay { get; set; }
                public string CustomNullText { get; set; }
                public string FormatString { get; set; }

                public LabelBinding(
                    Label target,
                    string columnName,
                    NullDisplayMode? nullDisplay = null,
                    string customNullText = null,
                    string formatString = null)
                {
                    Target = target ?? throw new ArgumentNullException(nameof(target));
                    ColumnName = columnName ?? throw new ArgumentNullException(nameof(columnName));
                    NullDisplay = nullDisplay;
                    CustomNullText = customNullText;
                    FormatString = formatString;
                }
            }

            // ────────────────────────────────────────────────────────────────────────────────
            // 1. Simple single-value binding (no map, no loader)
            // ────────────────────────────────────────────────────────────────────────────────
            public static void BindValue(
                this Label label,
                DataTable table,
                string columnName,
                string filterColumn = null,
                string filterValue = null,
                NullDisplayMode nullDisplay = NullDisplayMode.Empty,
                string customNullText = null,
                string formatString = null)
            {
                if (table == null || table.Rows.Count == 0)
                {
                    label.Text = GetNullDisplayText(nullDisplay, customNullText);
                    return;
                }

                DataRow row = null;

                if (!string.IsNullOrEmpty(filterColumn) && !string.IsNullOrEmpty(filterValue))
                {
                    var rows = table.Select($"{filterColumn} = '{filterValue.Replace("'", "''")}'");
                    row = rows.Length > 0 ? rows[0] : null;
                }
                else
                {
                    row = table.Rows[0]; // take first row if no filter
                }

                if (row == null)
                {
                    label.Text = GetNullDisplayText(nullDisplay, customNullText);
                    return;
                }

                object dbValue = row[columnName];
                long value = 0;
                string displayText;

                if (dbValue == null || dbValue == DBNull.Value)
                {
                    displayText = GetNullDisplayText(nullDisplay, customNullText);
                }
                else if (long.TryParse(dbValue.ToString(), out value))
                {
                    displayText = formatString != null ? value.ToString(formatString) : value.ToString();
                }
                else
                {
                    displayText = "–";
                }

                label.Text = displayText;
            }

            // ────────────────────────────────────────────────────────────────────────────────
            // 2. Multi-label binding (no loader)
            // ────────────────────────────────────────────────────────────────────────────────
            public static void BindDataToLabel(
                DataTable table,
                string keyColumn,
                IDictionary<string, List<LabelBinding>> rowMap,
                NullDisplayMode defaultNullDisplay = NullDisplayMode.Empty,
                Action<IDictionary<string, long>> onTotals = null)
            {
                if (table == null || rowMap == null || string.IsNullOrWhiteSpace(keyColumn))
                    return;

                var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                foreach (DataRow row in table.Rows)
                {
                    string key = row[keyColumn]?.ToString();
                    if (string.IsNullOrEmpty(key)) continue;

                    if (!rowMap.TryGetValue(key, out var bindings) || bindings == null)
                        continue;

                    foreach (var b in bindings)
                    {
                        object dbValue = row[b.ColumnName];
                        long value = 0;
                        string displayText;

                        if (dbValue == null || dbValue == DBNull.Value)
                        {
                            displayText = GetNullDisplayText(b.NullDisplay ?? defaultNullDisplay, b.CustomNullText);
                        }
                        else if (long.TryParse(dbValue.ToString(), out value))
                        {
                            displayText = b.FormatString != null ? value.ToString(b.FormatString) : value.ToString();
                        }
                        else
                        {
                            value = 0;
                            displayText = "–";
                        }

                        b.Target.Text = displayText;

                        totals[b.ColumnName] = totals.TryGetValue(b.ColumnName, out long acc) ? acc + value : value;
                    }
                }

                onTotals?.Invoke(totals);
            }

            // ────────────────────────────────────────────────────────────────────────────────
            // 3. Multi-label binding WITH loader (core logic + Task overload)
            // ────────────────────────────────────────────────────────────────────────────────
            public static async Task LoadLabelsAsync(
                Task<DataTable> dataTask,
                string keyColumn,
                IDictionary<string, List<LabelBinding>> rowMap,
                int revealDelayMs = 1000,
                NullDisplayMode defaultNullDisplay = NullDisplayMode.Empty,
                Action<IDictionary<string, long>> onTotals = null)
            {
                if (rowMap == null || rowMap.Count == 0)
                    return;

                // Start loading immediately
                foreach (var bindings in rowMap.Values)
                    foreach (var b in bindings)
                        b.Target.SetLoading(true);

                DataTable table;
                try
                {
                    table = await dataTask;
                }
                catch
                {
                    foreach (var bindings in rowMap.Values)
                        foreach (var b in bindings)
                        {
                            b.Target.Text = "Error";
                            b.Target.SetLoading(false);
                        }
                    return;
                }

                if (table == null)
                {
                    foreach (var bindings in rowMap.Values)
                        foreach (var b in bindings)
                            b.Target.SetLoading(false);
                    return;
                }

                await BindDataToLabelWithLoaderAsync(
                    table, keyColumn, rowMap, revealDelayMs, defaultNullDisplay, onTotals);
            }

            private static async Task BindDataToLabelWithLoaderAsync(
                    DataTable table,
                    string keyColumn,
                    IDictionary<string, List<LabelBinding>> rowMap,
                    int revealDelayMs,
                    NullDisplayMode defaultNullDisplay,
                    Action<IDictionary<string, long>> onTotals)
                {
                    if (table == null || rowMap == null || rowMap.Count == 0)
                        return;

                    // Start loading
                    foreach (var bindings in rowMap.Values)
                        foreach (var b in bindings)
                            b.Target.SetLoading(true);

                    if (revealDelayMs > 0)
                        await Task.Delay(revealDelayMs);

                    var totals = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

                    foreach (DataRow row in table.Rows)
                    {
                        string key = row[keyColumn]?.ToString();
                        if (string.IsNullOrEmpty(key)) continue;

                        if (!rowMap.TryGetValue(key, out var bindings) || bindings == null)
                            continue;

                        foreach (var b in bindings)
                        {
                            object dbValue = row[b.ColumnName];
                            long value = 0;
                            string displayText;

                            if (dbValue == null || dbValue == DBNull.Value)
                            {
                                displayText = GetNullDisplayText(b.NullDisplay ?? defaultNullDisplay, b.CustomNullText);
                            }
                            else if (long.TryParse(dbValue.ToString(), out value))
                            {
                                displayText = b.FormatString != null ? value.ToString(b.FormatString) : value.ToString();
                            }
                            else
                            {
                                value = 0;
                                displayText = "–";
                            }

                            b.Target.Text = displayText;

                            totals[b.ColumnName] = totals.TryGetValue(b.ColumnName, out long acc) ? acc + value : value;
                        }
                    }

                    onTotals?.Invoke(totals);

                    // Stop loading
                    foreach (var bindings in rowMap.Values)
                        foreach (var b in bindings)
                            b.Target.SetLoading(false);
                }

            public static List<LabelBinding> BindGroupLabel(
                IReadOnlyList<string> columns,
                params Label[] labels)
            {
                if (columns == null)
                    throw new ArgumentNullException(nameof(columns));

                if (labels == null)
                    throw new ArgumentNullException(nameof(labels));

                if (columns.Count != labels.Length)
                    throw new ArgumentException("Column and label count mismatch.");

                var list = new List<LabelBinding>(labels.Length);
                for (int i = 0; i < labels.Length; i++)
                    list.Add(new LabelBinding(labels[i], columns[i]));

                return list;
            }

            private static string GetNullDisplayText(NullDisplayMode mode, string customText)
                {
                    return mode switch
                    {
                        NullDisplayMode.Empty => "",
                        NullDisplayMode.Dash => "–",
                        NullDisplayMode.Zero => "0",
                        NullDisplayMode.Custom => customText ?? "",
                        _ => ""
                    };
                }

        #endregion

    }
}
