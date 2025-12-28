using Next.Common;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public static class Gridviews
    {
        // Overlay
        private const string Key = "__PRIME_OV__";

        #region Overlay

        public static void Overlay(this DataGridView grid, string text)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));

            // Remove previous handlers (prevents duplicates)
            grid.Paint -= PaintOverlay;
            grid.RowsAdded -= InvalidateGrid;
            grid.RowsRemoved -= InvalidateGrid;
            grid.DataSourceChanged -= InvalidateGrid;
            grid.Disposed -= GridDisposed;

            if (string.IsNullOrEmpty(text))
            {
                grid.Tag = null;
            }
            else
            {
                grid.Tag = Key + text;

                // Attach only when needed (optimized)
                grid.Paint += PaintOverlay;
                grid.RowsAdded += InvalidateGrid;
                grid.RowsRemoved += InvalidateGrid;
                grid.DataSourceChanged += InvalidateGrid;
                grid.Disposed += GridDisposed;
            }

            grid.Invalidate();
        }

        private static void PaintOverlay(object sender, PaintEventArgs e)
        {
            var grid = (DataGridView)sender;
            var tag = grid.Tag as string;

            if (tag == null || !tag.StartsWith(Key)) return;
            if (grid.Rows.Count > 0 && !grid.Rows[0].IsNewRow) return;

            string message = tag.Substring(Key.Length);

            // Reuse objects - zero garbage
            var rect = grid.ClientRectangle;
            rect.Inflate(-40, -40);

            var format = StringFormat.GenericDefault;
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;

            // EXACT same font & size as grid + perfect color
            using (var font = new Font(grid.Font, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.FromArgb(128, grid.ForeColor)))
            {
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                e.Graphics.DrawString(message, font, brush, rect, format);
            }
        }

        private static void InvalidateGrid(object sender, EventArgs e)
            => ((DataGridView)sender).Invalidate();

        private static void GridDisposed(object sender, EventArgs e)
        {
            var grid = (DataGridView)sender;
            grid.Paint -= PaintOverlay;
            grid.RowsAdded -= InvalidateGrid;
            grid.RowsRemoved -= InvalidateGrid;
            grid.DataSourceChanged -= InvalidateGrid;
            grid.Disposed -= GridDisposed;
        }

        #endregion

        #region Loading Message

        public static void DrawLoadingMessage(
        this DataGridView dgv,
        PaintEventArgs e,
        string message,
        Color? color = null)
            {
                if (dgv == null || dgv.IsDisposed || e == null)
                    return;

                if (!ShouldDraw(dgv))
                    return;

                using var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };

                e.Graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                using var brush = new SolidBrush(color ?? Color.Gray);

                e.Graphics.DrawString(
                    message,
                    dgv.Font,
                    brush,
                    dgv.ClientRectangle,
                    sf
                );
            }

        private static bool ShouldDraw(DataGridView dgv)
        {
            if (dgv.Rows.Count > 0)
                return false;

            if (dgv.DataSource != null)
                return false;

            return true;
        }

        #endregion

        #region Export|Import (Excel / Csv)

        /// <summary>
        /// Exports DataGridView content to CSV file (Excel compatible).
        /// </summary>
        /// <summary>
        /// Exports a DataGridView to CSV using a SaveFileDialog.
        /// </summary>
        /// <param name="dgv">Source DataGridView</param>
        /// <param name="defaultFileName">Suggested default file name</param>
        /// <param name="includeHeaders">Include column headers</param>
        public static void ToCsv(this DataGridView dgv, string defaultFileName = "Data.csv", bool includeHeaders = true)
                {
                    if (dgv == null) return;

                    using var sfd = new SaveFileDialog
                    {
                        Filter = "CSV File (*.csv)|*.csv",
                        FileName = defaultFileName,
                        DefaultExt = "csv",
                        AddExtension = true,
                        Title = "Save CSV File"
                    };

                    if (sfd.ShowDialog() != DialogResult.OK)
                        return;

                    using var sw = new StreamWriter(sfd.FileName);

                    if (includeHeaders)
                    {
                        var headers = dgv.Columns.Cast<DataGridViewColumn>()
                            .Select(c => $"\"{c.HeaderText.Replace("\"", "\"\"")}\"");
                        sw.WriteLine(string.Join(",", headers));
                    }

                    foreach (DataGridViewRow row in dgv.Rows)
                    {
                        if (row.IsNewRow) continue;
                        var cells = row.Cells.Cast<DataGridViewCell>()
                            .Select(c => $"\"{(c.Value ?? "").ToString().Replace("\"", "\"\"")}\"");
                        sw.WriteLine(string.Join(",", cells));
                    }
                }

        /// <summary>
        /// Imports a CSV file directly into a DataGridView.
        /// Opens OpenFileDialog for user to select file.
        /// Independent method; does not rely on other import helpers.
        /// </summary>
        /// <param name="dgv">Target DataGridView</param>
        /// <returns>Return indicating success or failure.</returns>
        public static IResult FromCsv(this DataGridView dgv)
        {
            if (dgv == null || dgv.IsDisposed)
                return Results.Error("Target DataGridView is null or disposed.");

            try
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "CSV (*.csv)|*.csv",
                    Title = "Select CSV File"
                };

                if (ofd.ShowDialog() != DialogResult.OK)
                    return Results.Error("CSV import canceled by user.");

                var lines = File.ReadAllLines(ofd.FileName);
                if (lines.Length == 0)
                    return Results.Error("CSV file is empty.");

                var dt = new DataTable(Path.GetFileNameWithoutExtension(ofd.FileName));

                // Columns from first line
                var headers = lines[0].Split(',').Select(h => h.Trim('"')).ToArray();
                foreach (var header in headers)
                    dt.Columns.Add(header);

                // Data rows
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = lines[i].Split(',').Select(v => v.Trim('"')).ToArray();
                    var row = dt.NewRow();
                    for (int c = 0; c < headers.Length && c < values.Length; c++)
                        row[c] = values[c];
                    dt.Rows.Add(row);
                }

                // Bind to DataGridView
                dgv.SuspendLayout();
                try
                {
                    dgv.Columns.Clear();
                    dgv.DataSource = dt;
                    dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                }
                finally
                {
                    dgv.ResumeLayout();
                }

                return Results.Ok("CSV imported to DataGridView successfully.");
            }
            catch (Exception ex)
            {
                return Results.Error($"Failed to import CSV: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports a DataGridView to Excel (.xlsx) using a SaveFileDialog and EPPlus.
        /// </summary>
        /// <param name="dgv">Source DataGridView</param>
        /// <param name="defaultFileName">Suggested default file name</param>
        /// <param name="sheetName">Worksheet name</param>
        /// <param name="includeHeaders">Include column headers</param>
        /// <param name="autoFit">Auto-fit columns</param>
        /// <returns>Result indicating success or failure.</returns>
        public static IResult ToExcel(this DataGridView dgv, string defaultFileName = "Data.xlsx",
                    string sheetName = "Details", bool includeHeaders = true, bool autoFit = true)
        {
            try
            {
                if (dgv == null)
                    return Results.Error("DataGridView is null.");

                using var sfd = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = defaultFileName,
                    DefaultExt = "xlsx",
                    AddExtension = true,
                    Title = "Save Excel File"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                    return Results.Error("Export cancelled by user.");

                ExcelPackage.License.SetNonCommercialPersonal("Next");

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add(sheetName);

                int rowIndex = 1;
                int colCount = dgv.Columns.Count;

                // Headers
                if (includeHeaders)
                {
                    for (int c = 0; c < colCount; c++)
                    {
                        ws.Cells[rowIndex, c + 1].Value = dgv.Columns[c].HeaderText;
                        ws.Cells[rowIndex, c + 1].Style.Font.Bold = true;
                    }
                    rowIndex++;
                }

                // Data
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;

                    for (int c = 0; c < colCount; c++)
                        ws.Cells[rowIndex, c + 1].Value = row.Cells[c].Value;

                    rowIndex++;
                }

                // Formatting
                if (includeHeaders)
                {
                    using var range = ws.Cells[1, 1, 1, colCount];
                    range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                }

                if (autoFit && ws.Dimension != null)
                    ws.Cells[ws.Dimension.Address].AutoFitColumns();

                // Save
                var fi = new FileInfo(sfd.FileName);
                if (fi.Exists) fi.Delete();
                package.SaveAs(fi);

                return Results.Ok($"Export completed successfully: {fi.FullName}");
            }
            catch (Exception ex)
            {
                return Results.Error($"Export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports an Excel file (.xlsx) into a DataGridView.
        /// Opens OpenFileDialog for user to select file.
        /// Independent from any DataTable import method.
        /// </summary>
        /// <param name="dgv">Target DataGridView</param>
        /// <param name="sheetName">Optional worksheet name. If null, first sheet is used.</param>
        /// <returns>Result indicating success or failure.</returns>
        public static IResult FromExcel(this DataGridView dgv, string sheetName = null)
        {
            if (dgv == null || dgv.IsDisposed)
                return Results.Error("Target DataGridView is null or disposed.");

            try
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    Title = "Select Excel File"
                };

                if (ofd.ShowDialog() != DialogResult.OK)
                    return Results.Error("Excel import canceled by user.");

                var file = new FileInfo(ofd.FileName);
                if (!file.Exists)
                    return Results.Error("Selected file does not exist.");

                ExcelPackage.License.SetNonCommercialPersonal("Next");

                using var package = new ExcelPackage(file);
                var ws = !string.IsNullOrWhiteSpace(sheetName)
                    ? package.Workbook.Worksheets[sheetName]
                    : package.Workbook.Worksheets.FirstOrDefault();

                if (ws == null)
                    return Results.Error("No worksheet found in the Excel file.");

                var dt = new DataTable(ws.Name);

                // Add columns from first row
                foreach (var firstRowCell in ws.Cells[1, 1, 1, ws.Dimension.End.Column])
                    dt.Columns.Add(firstRowCell.Text);

                // Add rows
                for (int rowNum = 2; rowNum <= ws.Dimension.End.Row; rowNum++)
                {
                    var wsRow = ws.Cells[rowNum, 1, rowNum, ws.Dimension.End.Column];
                    var row = dt.NewRow();
                    int colIndex = 0;
                    foreach (var cell in wsRow)
                        row[colIndex++] = cell.Value ?? DBNull.Value;

                    dt.Rows.Add(row);
                }

                // Bind to DataGridView
                dgv.SuspendLayout();
                try
                {
                    dgv.Columns.Clear();
                    dgv.DataSource = dt;
                    dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
                }
                finally
                {
                    dgv.ResumeLayout();
                }

                return Results.Ok("Excel file imported to DataGridView successfully.");
            }
            catch (Exception ex)
            {
                return Results.Error($"Failed to import Excel file: {ex.Message}");
            }
        }

        #endregion

        #region Show Row Numbers in Row Header

        /// <summary>
        /// Displays row numbers (1-based) in the row headers.
        /// </summary>
        public static void ShowRowNumbers(this DataGridView dgv)
            {
                if (dgv == null) return;

                dgv.RowPostPaint += (s, e) =>
                {
                    var grid = s as DataGridView;
                    var rowIdx = (e.RowIndex + 1).ToString();

                    var centerFormat = new StringFormat()
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };

                    var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
                    e.Graphics.DrawString(rowIdx, grid.RowHeadersDefaultCellStyle.Font,
                        new SolidBrush(grid.RowHeadersDefaultCellStyle.ForeColor),
                        headerBounds, centerFormat);
                };
            }

        #endregion

        #region Auto-Resize Columns to Fit Content

        /// <summary>
        /// Auto-sizes all columns to fit content (call after data load).
        /// </summary>
        public static void AutoResizeColumnsToContent(this DataGridView dgv, DataGridViewAutoSizeColumnsMode mode = DataGridViewAutoSizeColumnsMode.AllCells)
        {
            dgv.AutoResizeColumns(mode);
        }

        #endregion

        #region Highlight Modified/Dirty Rows

        private static Color _dirtyRowColor = Color.LightYellow;

        /// <summary>
        /// Highlights rows that have been edited (useful for tracking changes).
        /// Works with bound or unbound grids.
        /// </summary>
        public static void HighlightDirtyRows(this DataGridView dgv, Color highlightColor = default)
        {
            if (highlightColor != default) _dirtyRowColor = highlightColor;

            dgv.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0)
                    dgv.Rows[e.RowIndex].DefaultCellStyle.BackColor = _dirtyRowColor;
            };

            dgv.RowsAdded += (s, e) =>
            {
                for (int i = e.RowIndex; i < e.RowIndex + e.RowCount; i++)
                    dgv.Rows[i].DefaultCellStyle.BackColor = _dirtyRowColor;
            };
        }

        #endregion

        #region Excel-Like Copy/Paste (Multi-Cell Support)

        /// <summary>
            /// Overrides default copy (Ctrl+C) to copy with tabs (Excel compatible).
            /// Paste works as usual.
            /// </summary>
        public static void EnableExcelCopyPaste(this DataGridView dgv)
            {
                dgv.KeyDown += (s, e) =>
                {
                    if (e.Control && e.KeyCode == Keys.C)
                    {
                        DataObject d = dgv.GetClipboardContent();
                        Clipboard.SetDataObject(d);
                        e.Handled = true;
                    }
                };
            }

        #endregion

        #region Bind Data

        private static void BindTable(DataGridView dgv, DataTable dt)
        {
            dgv.SuspendLayout();
            dgv.Rows.Clear();

            if (dt == null || dt.Rows.Count == 0)
            {
                dgv.ClearSelection();
                dgv.ResumeLayout();
                return;
            }

            // AUTO-CREATE COLUMNS ONLY IF GRID HAS NONE
            if (dgv.Columns.Count == 0)
            {
                foreach (DataColumn col in dt.Columns)
                    dgv.Columns.Add(col.ColumnName, col.ColumnName);
            }

            int colCount = Math.Min(dt.Columns.Count, dgv.Columns.Count);

            foreach (DataRow row in dt.Rows)
            {
                int idx = dgv.Rows.Add();
                for (int i = 0; i < colCount; i++)
                    dgv.Rows[idx].Cells[i].Value = row[i];
            }

            dgv.ClearSelection();
            dgv.ResumeLayout();
        }

        private static void BindDynamic(DataGridView dgv, IEnumerable<dynamic> rows)
        {
            dgv.SuspendLayout();
            dgv.Rows.Clear();

            var list = rows?.ToList();
            if (list == null || list.Count == 0)
            {
                dgv.ClearSelection();
                dgv.ResumeLayout();
                return;
            }

            // AUTO-CREATE COLUMNS ONLY IF GRID HAS NONE
            if (dgv.Columns.Count == 0)
            {
                var first = (IDictionary<string, object>)list[0];
                foreach (var key in first.Keys)
                    dgv.Columns.Add(key, key);
            }

            foreach (var row in list)
            {
                var values = ((IDictionary<string, object>)row).Values.ToArray();
                int idx = dgv.Rows.Add();

                for (int i = 0; i < dgv.Columns.Count && i < values.Length; i++)
                    dgv.Rows[idx].Cells[i].Value = values[i];
            }

            dgv.ClearSelection();
            dgv.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            dgv.ResumeLayout();
        }

        /// <summary>
        /// Unified binding entry point.
        /// Supports DataTable, IEnumerable<dynamic>, IEnumerable<IDictionary<string, object>>.
        /// </summary>
        public static IResult BindData(this DataGridView dgv, object data)
        {
            if (dgv == null || dgv.IsDisposed || dgv.Disposing)
                return Results.Error("DataGridView is null or disposed.");

            try
            {
                if (data == null)
                {
                    dgv.Rows.Clear();
                    dgv.ClearSelection();
                    return Results.Ok("Grid cleared.");
                }

                if (data is DataTable dt)
                {
                    BindTable(dgv, dt);
                    return Results.Ok("DataTable bound.");
                }

                if (data is IEnumerable<dynamic> dyn)
                {
                    BindDynamic(dgv, dyn);
                    return Results.Ok("Dynamic data bound.");
                }

                if (data is IEnumerable<IDictionary<string, object>> dict)
                {
                    BindDynamic(dgv, dict.Cast<dynamic>());
                    return Results.Ok("Dictionary data bound.");
                }

                return Results.Error("Unsupported data type.");
            }
            catch (Exception ex)
            {
                return Results.Error($"Bind failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Binds data to a DataGridView with a loading spinner and optional message.
        /// Uses the optimized BindData method internally.
        /// </summary>
        /// <param name="dgv">Target DataGridView</param>
        /// <param name="data">Data to bind (DataTable, IEnumerable&lt;dynamic&gt;, or IEnumerable&lt;IDictionary&lt;string, object&gt;&gt;)</param>
        /// <param name="message">Optional loading message</param>
        /// <param name="spinnerColor">Optional spinner color</param>
        /// <param name="spinnerRadius">Optional spinner size</param>
        /// <returns>IResult indicating success or failure</returns>
        public static async Task<IResult> BindDataWithLoaderAsync<T>(
          this DataGridView dgv,
          Task<T> dataTask,
          string loadingMessage = "Loading ...",
          string emptyMessage = "No Data Found.",
          Color? spinnerColor = null,
          int spinnerRadius = 10,
          int minSpinnerMilliseconds = 1500,
          LoaderType loaderType = LoaderType.Spinner,
          int skeletonRows = 8)
        {
            if (dgv == null || dgv.IsDisposed || dgv.Disposing)
                return Results.Error("DataGridView is null or disposed.");

            dgv.EnabledBuffer();

            // Show initial loader
            dgv.SetLoading(true, loadingMessage, spinnerColor, spinnerRadius, loaderType, skeletonRows);

            try
            {
                // Run data task and minimum spinner delay
                var delayTask = Task.Delay(minSpinnerMilliseconds);
                var data = await dataTask;
                await delayTask;

                var result = dgv.BindData(data);

                // Show text-only message if no rows, otherwise hide overlay
                if (dgv.Rows.Count == 0)
                {
                    dgv.SetLoading(true, emptyMessage, spinnerColor, spinnerRadius: 0, LoaderType.MessageOnly);
                }
                else
                {
                    dgv.SetLoading(false);
                }

                return result;
            }
            finally
            {
                // Ensure overlay is removed if data exists
                if (dgv != null && !dgv.IsDisposed && !dgv.Disposing && dgv.Rows.Count > 0)
                    dgv.SetLoading(false);
            }
        }


        #endregion

        #region Loading Animation – DataGridView Overlay (Spinner / Skeleton / Message)

        private static readonly HashSet<DataGridView> ActiveGrids = new();
        private static readonly HashSet<DataGridView> DirtyGrids = new();
        private static Timer _timer;
        private static float _angle;

        public enum LoaderType
        {
            Spinner,
            Skeleton,
            MessageOnly
        }

        private static class LoaderConfig
        {
            // ── Easy-to-tune global defaults ───────────────────────────────────────

            public static int TimerIntervalMs { get; } = 45;                // smooth & low CPU

            // Spinner Settings
            public static int DefaultSpinnerRadius { get; } = 20;          // increased default for better spacing
            public static float SpinnerAngleStep { get; } = 12f;            // degrees per tick
            public static int SpinnerDotDiameter { get; } = 6;
            public static Color DefaultColor { get; } = Color.DimGray;

            // Skeleton Settings
            public static int DefaultSkeletonRows { get; } = 8;
            public static float SkeletonShimmerSpeed { get; } = 5.5f;
            public static float SkeletonStaggerFactor { get; } = 0.7f;
            public static float SkeletonLoopWidth { get; } = 800f;
            public static Color SkeletonFromColor { get; } = Color.LightGray;
            public static Color SkeletonToColor { get; } = Color.WhiteSmoke;
            public static int SkeletonCornerRadius { get; } = 6;
            public static int SkeletonCellGap { get; } = 15;
            public static float SkeletonOpacity { get; } = 0.65f;

            // ────────────────────────────────────────────────────────────────────────
        }

        private class LoaderState
        {
            public string Message { get; set; }
            public LoaderType LoaderType { get; set; }
            public int SpinnerRadius { get; set; }
            public Color Color { get; set; }
            public int SkeletonRows { get; set; }
            public float[] SkeletonOffsets { get; set; }
        }

        public static void SetLoading(
            this DataGridView dgv,
            bool loading,
            string message = null,
            Color? color = null,
            int? spinnerRadius = null,
            LoaderType loaderType = LoaderType.Spinner,
            int? skeletonRows = null)
        {
            if (dgv == null || dgv.IsDisposed) return;

            // Clean up old handlers
            dgv.Paint -= Grid_Paint;
            dgv.Paint -= Grid_PaintSkeleton;

            if (!loading)
            {
                ActiveGrids.Remove(dgv);
                dgv.Tag = null;
                DirtyGrids.Remove(dgv);
                if (ActiveGrids.Count == 0) StopTimer();
                dgv.Invalidate();
                return;
            }

            // Enable double-buffering for smooth animation
            dgv.EnabledBuffer();

            var state = new LoaderState
            {
                Message = message,
                LoaderType = loaderType,
                SpinnerRadius = spinnerRadius ?? LoaderConfig.DefaultSpinnerRadius,
                Color = color ?? LoaderConfig.DefaultColor,
                SkeletonRows = skeletonRows ?? LoaderConfig.DefaultSkeletonRows,
            };

            if (loaderType == LoaderType.Skeleton)
            {
                state.SkeletonOffsets = new float[state.SkeletonRows];
                for (int i = 0; i < state.SkeletonRows; i++)
                    state.SkeletonOffsets[i] = -80f * (i % 4); // gentle stagger
            }

            dgv.Tag = state;
            ActiveGrids.Add(dgv);

            switch (loaderType)
            {
                case LoaderType.Spinner:
                case LoaderType.MessageOnly:
                    dgv.Paint += Grid_Paint;
                    break;
                case LoaderType.Skeleton:
                    dgv.Paint += Grid_PaintSkeleton;
                    break;
            }

            StartTimer();
            dgv.Invalidate();
        }

        private static void StartTimer()
        {
            if (_timer != null) return;
            _timer = new Timer { Interval = LoaderConfig.TimerIntervalMs };
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
            _angle = (_angle + LoaderConfig.SpinnerAngleStep) % 360f;

            DirtyGrids.Clear();

            foreach (var dgv in ActiveGrids.ToList())
            {
                if (dgv.IsDisposed || !dgv.Visible)
                {
                    ActiveGrids.Remove(dgv);
                    continue;
                }

                if (dgv.Tag is not LoaderState state) continue;

                bool needsRepaint = state.LoaderType == LoaderType.Spinner || state.LoaderType == LoaderType.MessageOnly;

                if (state.LoaderType == LoaderType.Skeleton && state.SkeletonOffsets is { } offsets)
                {
                    for (int r = 0; r < offsets.Length; r++)
                    {
                        offsets[r] = (offsets[r] + LoaderConfig.SkeletonShimmerSpeed + r * LoaderConfig.SkeletonStaggerFactor)
                                     % LoaderConfig.SkeletonLoopWidth;
                    }
                    needsRepaint = true;
                }

                if (needsRepaint)
                    DirtyGrids.Add(dgv);
            }

            foreach (var dgv in DirtyGrids)
                dgv.Invalidate();

            if (ActiveGrids.Count == 0)
                StopTimer();
        }

        private static void Grid_Paint(object sender, PaintEventArgs e)
        {
            if (sender is not DataGridView dgv || dgv.IsDisposed || !ActiveGrids.Contains(dgv)) return;
            if (dgv.Tag is not LoaderState state) return;

            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var rect = dgv.ClientRectangle;
            string text = state.Message ?? "";

            if (state.SpinnerRadius > 0 && state.LoaderType == LoaderType.Spinner)
            {
                int gap = string.IsNullOrEmpty(text) ? 0 : 8;
                var textSize = string.IsNullOrEmpty(text) ? SizeF.Empty : e.Graphics.MeasureString(text, dgv.Font);
                float totalWidth = state.SpinnerRadius * 2 + gap + textSize.Width;

                var center = new Point(
                    (int)(rect.Left + (rect.Width - totalWidth) / 2 + state.SpinnerRadius),
                    rect.Height / 2 - 1);

                DrawSpinner(e.Graphics, center, state.SpinnerRadius, _angle, state.Color);

                if (!string.IsNullOrEmpty(text))
                {
                    using var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    var textRect = new RectangleF(center.X + state.SpinnerRadius + gap, 0, textSize.Width, rect.Height);
                    using var brush = new SolidBrush(state.Color);
                    e.Graphics.DrawString(text, dgv.Font, brush, textRect, sf);
                }
            }
            else if (!string.IsNullOrEmpty(text))
            {
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                using var brush = new SolidBrush(state.Color);
                e.Graphics.DrawString(text, dgv.Font, brush, rect, sf);
            }
        }

        private static void DrawSpinner(Graphics g, Point center, int radius, float angle, Color color)
        {
            const int dots = 8;
            int dotDiameter = LoaderConfig.SpinnerDotDiameter;
            int half = dotDiameter / 2;

            for (int i = 0; i < dots; i++)
            {
                float a = angle + i * (360f / dots);
                double rad = a * (Math.PI / 180d);
                int x = center.X + (int)(Math.Cos(rad) * radius);
                int y = center.Y + (int)(Math.Sin(rad) * radius);

                int alpha = (int)(255f * i / dots);

                using var brush = new SolidBrush(Color.FromArgb(alpha, color));
                g.FillEllipse(brush, x - half, y - half, dotDiameter, dotDiameter);
            }
        }

        private static void Grid_PaintSkeleton(object sender, PaintEventArgs e)
        {
            if (sender is not DataGridView dgv || dgv.IsDisposed || !ActiveGrids.Contains(dgv)) return;
            if (dgv.Tag is not LoaderState state || state.SkeletonOffsets == null) return;

            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;

            int rowHeight = dgv.RowTemplate.Height;
            int y = dgv.ColumnHeadersHeight + 2;
            int cellHeight = rowHeight - LoaderConfig.SkeletonCellGap;
            int totalWidth = dgv.ClientSize.Width - 4;
            int cornerRadius = LoaderConfig.SkeletonCornerRadius;

            using var baseBrush = new LinearGradientBrush(
                new Rectangle(0, 0, totalWidth, cellHeight),
                Color.FromArgb((int)(255 * LoaderConfig.SkeletonOpacity), LoaderConfig.SkeletonFromColor),
                Color.FromArgb((int)(255 * LoaderConfig.SkeletonOpacity), LoaderConfig.SkeletonToColor),
                10f);

            var blend = new Blend
            {
                Positions = new[] { 0.0f, 0.35f, 0.50f, 0.65f, 1.0f },
                Factors = new[] { 0.0f, 0.0f, 1.0f, 0.0f, 0.0f }
            };
            baseBrush.Blend = blend;

            for (int r = 0; r < state.SkeletonRows; r++)
            {
                var rect = new Rectangle(2, y, totalWidth, cellHeight);
                float offset = state.SkeletonOffsets[r];

                using var brush = (LinearGradientBrush)baseBrush.Clone();
                brush.TranslateTransform(offset, 0);

                using var path = RoundedRect(rect, cornerRadius);
                e.Graphics.FillPath(brush, path);

                y += rowHeight;
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            var path = new GraphicsPath();
            int diameter = radius * 2;
            var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        #endregion


    }

}

