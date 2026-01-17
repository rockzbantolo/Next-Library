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
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    public static class Gridviews
    {
        // Overlay
        private const string Key = "__PRIME_OV__";

        #region Getters

            /// <summary>
            /// Gets a typed value from a specific row and column in a DataGridView.
            /// Safe against null, DBNull, missing row/column, conversion errors.
            ///
            /// Typical usage in CellDoubleClick:
            ///   string id = dgv.GetValue<string>(e.RowIndex, "Id");
            ///   int qty  = dgv.GetValue<int>(e.RowIndex, "Quantity", 0);
            /// </summary>
            /// <typeparam name="T">The expected type (string, int, decimal, DateTime, bool, etc.)</typeparam>
            /// <param name="dgv">The DataGridView instance</param>
            /// <param name="rowIndex">Row index (usually e.RowIndex from CellDoubleClick)</param>
            /// <param name="columnName">Column name as set in the designer (Name property)</param>
            /// <param name="defaultValue">Value returned if the cell is missing, null, DBNull or conversion fails</param>
            /// <returns>The converted value or defaultValue</returns>
            public static T GetValue<T>(this DataGridView dgv, int rowIndex, string columnName, T defaultValue = default)
            {
                if (dgv is null ||
                    rowIndex < 0 ||
                    rowIndex >= dgv.RowCount ||
                    string.IsNullOrWhiteSpace(columnName))
                {
                    return defaultValue;
                }

                var cell = dgv.Rows[rowIndex]?.Cells[columnName];
                if (cell is null)
                {
                    return defaultValue;
                }

                var value = cell.Value;
                if (value is null || value == DBNull.Value)
                {
                    return defaultValue;
                }

                try
                {
                    // Most common conversions handled automatically
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    // If type mismatch or invalid format → return default
                    return defaultValue;
                }
            }

            /// <summary>
            /// Returns a dictionary of column names → values for the given row.
            /// </summary>
            public static Dictionary<string, object> GetRowValues(this DataGridView dgv, int rowIndex)
            {
                var dict = new Dictionary<string, object>();

                if (dgv == null || rowIndex < 0 || rowIndex >= dgv.RowCount)
                    return dict;

                var row = dgv.Rows[rowIndex];
                foreach (DataGridViewCell cell in row.Cells)
                {
                    string colName = dgv.Columns[cell.ColumnIndex].Name;
                    dict[colName] = cell.Value ?? DBNull.Value;
                }

                return dict;
            }

            public static bool TryGetValue<T>(this DataGridView dgv, int rowIndex, string columnName, out T value)
            {
                value = default;

                if (dgv == null || rowIndex < 0 || rowIndex >= dgv.RowCount || string.IsNullOrWhiteSpace(columnName))
                    return false;

                var cellValue = dgv.Rows[rowIndex].Cells[columnName]?.Value;
                if (cellValue == null || cellValue == DBNull.Value)
                    return false;

                try
                {
                    value = (T)Convert.ChangeType(cellValue, typeof(T));
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            /// <summary>
            /// Applies custom cell styling for a specific column based on a rules dictionary.
            /// </summary>
            /// <param name="dgv">Target DataGridView.</param>
            /// <param name="e">CellFormattingEventArgs from CellFormatting event.</param>
            /// <param name="columnName">Column header to apply styles.</param>
            /// <param name="rules">Dictionary of status → (BackColor, ForeColor).</param>
            public static void ApplyStatusStyle(this DataGridView dgv, DataGridViewCellFormattingEventArgs e, string columnName, IReadOnlyDictionary<string, (Color back, Color fore)> rules)
            {
                if (e.Value == null || e.ColumnIndex < 0)
                    return;

                var column = dgv.Columns[e.ColumnIndex];
                if (column == null || column.HeaderText != columnName)
                    return;

                string status = e.Value.ToString().Trim();
                if (!rules.TryGetValue(status, out var colors))
                    colors = (Color.White, Color.Black);

                // Apply colors
                var style = e.CellStyle;
                style.BackColor = colors.back;
                style.ForeColor = colors.fore;
                style.SelectionBackColor = colors.back;
                style.SelectionForeColor = colors.fore;
            }

        #endregion

        #region Overlay

        // ────────────────────────────────────────────────────────────────────────────────
        // Displays Message Using Paint
        // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            // 
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            //  Exports DataGridView content to CSV file (Excel compatible).
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            //  
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            //  
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            //  
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            //  
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            //  
            // ────────────────────────────────────────────────────────────────────────────────
            /// <summary>
            /// Auto-sizes all columns to fit content (call after data load).
            /// </summary>
            public static void AutoResizeColumnsToContent(this DataGridView dgv, DataGridViewAutoSizeColumnsMode mode = DataGridViewAutoSizeColumnsMode.AllCells)
            {
                dgv.AutoResizeColumns(mode);
            }

        #endregion

        #region Highlight Modified/Dirty Rows

            // ────────────────────────────────────────────────────────────────────────────────
            //  
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            //  
            // ────────────────────────────────────────────────────────────────────────────────
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

            // ────────────────────────────────────────────────────────────────────────────────
            // MAIN ENTRY POINT
            // ────────────────────────────────────────────────────────────────────────────────
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

                    // Support IEnumerable<T> models
                    var type = data.GetType();
                    if (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
                    {
                        var modelType = type.GetGenericArguments()[0];
                        var method = typeof(Gridviews)
                            .GetMethod(nameof(BindModel), BindingFlags.NonPublic | BindingFlags.Static)
                            ?.MakeGenericMethod(modelType);

                        method?.Invoke(null, new object[] { dgv, data });
                        return Results.Ok("Model bound.");
                    }

                    return Results.Error("Unsupported data type.");
                }
                catch (Exception ex)
                {
                    return Results.Error($"Bind failed: {ex.Message}");
                }
            }

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

            private static void BindModel<T>(DataGridView dgv, IEnumerable<T> rows)
            {
                var list = rows?.ToList();
                dgv.SuspendLayout();
                dgv.Rows.Clear();

                if (list == null || list.Count == 0)
                {
                    dgv.ClearSelection();
                    dgv.ResumeLayout();
                    return;
                }

                var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                // Create columns if non exist
                if (dgv.Columns.Count == 0)
                {
                    dgv.AutoGenerateColumns = false;

                    foreach (var p in props)
                    {
                        dgv.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            Name = p.Name,
                            HeaderText = p.Name,
                            DataPropertyName = p.Name,
                            AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells
                        });
                    }
                }

                // Fill rows matching columns by Name
                foreach (var item in list)
                {
                    int rowIndex = dgv.Rows.Add();
                    foreach (DataGridViewColumn col in dgv.Columns)
                    {
                        var prop = props.FirstOrDefault(p => p.Name == col.Name);
                        if (prop != null)
                            dgv.Rows[rowIndex].Cells[col.Index].Value = prop.GetValue(item);
                    }
                }

                dgv.ClearSelection();
                dgv.ResumeLayout();
            }

            /// <summary>
            /// Extension method for DataGridView that shows a loading overlay, awaits data asynchronously,
            /// applies minimum display time, binds the data, and shows either an empty message or hides the overlay.
            /// </summary>
            /// <typeparam name="T">Type of data being bound (usually DataTable or similar)</typeparam>
            /// <param name="dgv">The DataGridView to bind to</param>
            /// <param name="dataTask">Asynchronous task that fetches the data</param>
            /// <param name="loadingMessage">Message shown during loading (default: "Loading ...")</param>
            /// <param name="emptyMessage">Message shown when no data is returned (default: "No Data Found.")</param>
            /// <param name="spinnerColor">Custom color for spinner (optional)</param>
            /// <param name="spinnerRadius">Radius of the spinner in pixels (default: 10)</param>
            /// <param name="minSpinnerMilliseconds">Minimum time (ms) to show the loading overlay (default: 1500)</param>
            /// <param name="loaderType">Style of loading indicator (default: Spinner)</param>
            /// <param name="skeletonRows">Number of skeleton rows when using Skeleton mode (default: 8)</param>
            /// <returns>IResult from the BindData operation</returns>
            public static async Task<IResult> BindDataWithLoaderAsync<T>(
                    this DataGridView dgv,
                    Task<T> dataTask,
                    string loadingMessage = "Loading ...",
                    string emptyMessage = "No Data Found.",
                    Color? spinnerColor = null,
                    int spinnerRadius = 10,
                    int minSpinnerMilliseconds = 500,
                    GridLoaderType loaderType = GridLoaderType.Spinner,
                    int skeletonRows = 8,
                    Color? skeletonBaseColor = null,
                    Color? skeletonHighlightColor = null)
                {
                    if (dgv == null || dgv.IsDisposed || dgv.Disposing)
                        return Results.Error("DataGridView is null or disposed.");

                    // Show initial loader with all customization options
                    dgv.SetLoading(
                        isLoading: true,
                        message: loadingMessage,
                        loaderType: loaderType,
                        customColor: spinnerColor,
                        spinnerRadius: spinnerRadius,
                        skeletonRowCount: skeletonRows,
                        skeletonBaseColor: skeletonBaseColor,
                        skeletonHighlightColor: skeletonHighlightColor);

                    dgv.Rows.Clear();

                    try
                    {
                        var minimumDelayTask = Task.Delay(minSpinnerMilliseconds);
                        var data = await dataTask;
                        await minimumDelayTask;
                        var result = dgv.BindData(data);

                        if (dgv.Rows.Count == 0)
                        {
                            dgv.SetLoading(
                                isLoading: true,
                                message: emptyMessage,
                                loaderType: GridLoaderType.MessageOnly,
                                customColor: spinnerColor,
                                spinnerRadius: 0);
                        }
                        else
                        {
                            dgv.SetLoading(false);
                        }

                        return result;
                    }
                    finally
                    {
                        if (dgv is not null && !dgv.IsDisposed && !dgv.Disposing && dgv.Rows.Count > 0)
                            dgv.SetLoading(false);
                    }
                }

            // ────────────────────────────────────────────────────────────────────────────────
            // Load All Gridview at Once
            // ────────────────────────────────────────────────────────────────────────────────
            public sealed class GridLoaderBatch
            {
                private readonly List<Task> _tasks = new();

                public GridLoaderBatch Add(
                    DataGridView grid,
                    Task<DataTable> dataTask,
                    string loading,
                    string empty)
                {
                    _tasks.Add(
                        grid.BindDataWithLoaderAsync(
                            dataTask,
                            loadingMessage: loading,
                            emptyMessage: empty,
                            loaderType: GridLoaderType.Skeleton
                        )
                    );
                    return this;
                }

                public Task RunAsync() => Task.WhenAll(_tasks);
            }

        #endregion

        #region DataGridView Loading Overlay

            // ── Private constants ───────────────────────────────

            private const int TimerIntervalMs = 45;
            private const float SpinnerAngleStep = 12f;
            private const int SpinnerDotCount = 12;
            private const int SpinnerDotDiameter = 6;

            private const float SkeletonShimmerSpeed = 6f;
            private const float SkeletonStaggerFactor = 0.8f;
            private const float SkeletonLoopWidth = 900f;
            private const int SkeletonCellPadding = 12;
            private const int SkeletonCornerRadius = 6;
            private const float SkeletonOpacity = 0.65f;

            // ─────────────────────────────────────────────────────

            /// <summary>
            /// Types of loading visuals supported.
            /// </summary>
            public enum GridLoaderType
            {
                Spinner,
                Skeleton,
                MessageOnly
            }

            /// <summary>
            /// Per-grid loading state
            /// </summary>
            private class GridLoaderState
            {
                public string Message { get; set; }
                public GridLoaderType LoaderType { get; set; }
                public int SpinnerRadius { get; set; }
                public Color SpinnerColor { get; set; }
                public int SkeletonRowCount { get; set; }
                public float[] SkeletonOffsets { get; set; }
                public Color SkeletonBaseColor { get; set; }
                public Color SkeletonHighlightColor { get; set; }
                public float[] SkeletonWidths { get; set; }
            }

            /// <summary>
            /// Manages active grids, timer, and animation state (fully private)
            /// </summary>
            private static class GridLoaderManager
            {
                private static readonly HashSet<DataGridView> ActiveGrids = new();
                private static readonly HashSet<DataGridView> DirtyGrids = new();
                private static Timer _timer;
                private static float _currentAngle;

                public static void Add(DataGridView dgv, GridLoaderState state)
                {
                    ActiveGrids.Add(dgv);
                    dgv.Tag = state;
                    StartTimerIfNeeded();
                    dgv.Invalidate();
                }

                public static void Remove(DataGridView dgv)
                {
                    ActiveGrids.Remove(dgv);
                    DirtyGrids.Remove(dgv);
                    dgv.Tag = null;
                    if (ActiveGrids.Count == 0) StopTimer();
                    dgv.Invalidate();
                }

                private static void StartTimerIfNeeded()
                {
                    if (_timer != null) return;
                    _timer = new Timer { Interval = TimerIntervalMs };
                    _timer.Tick += (_, _) => TimerTick();
                    _timer.Start();
                }

                private static void StopTimer()
                {
                    if (_timer == null) return;
                    _timer.Stop();
                    _timer.Dispose();
                    _timer = null;
                }

                private static void TimerTick()
                {
                    _currentAngle = (_currentAngle + SpinnerAngleStep) % 360f;
                    DirtyGrids.Clear();

                    foreach (var dgv in ActiveGrids.ToList())
                    {
                        if (dgv.IsDisposed || !dgv.Visible)
                        {
                            ActiveGrids.Remove(dgv);
                            continue;
                        }

                        if (dgv.Tag is not GridLoaderState state) continue;

                        bool needsRedraw = state.LoaderType is GridLoaderType.Spinner or GridLoaderType.MessageOnly;

                        if (state.LoaderType == GridLoaderType.Skeleton && state.SkeletonOffsets != null)
                        {
                            for (int i = 0; i < state.SkeletonOffsets.Length; i++)
                            {
                                state.SkeletonOffsets[i] = (state.SkeletonOffsets[i] + SkeletonShimmerSpeed +
                                                           i * SkeletonStaggerFactor) % SkeletonLoopWidth;
                            }
                            needsRedraw = true;
                        }

                        if (needsRedraw)
                            DirtyGrids.Add(dgv);
                    }

                    foreach (var dgv in DirtyGrids)
                        dgv.Invalidate();

                    if (ActiveGrids.Count == 0)
                        StopTimer();
                }

                public static float CurrentAngle => _currentAngle;
            }

            /// <summary>
            /// Shows or hides a loading overlay on the DataGridView.
            /// </summary>
            /// <param name="dgv">The grid to apply loading to</param>
            /// <param name="isLoading">true = show overlay, false = hide</param>
            /// <param name="message">Optional message (default: "Loading...")</param>
            /// <param name="loaderType">Spinner (default), Skeleton, or MessageOnly</param>
            /// <param name="customColor">Color for spinner/text (default: DimGray)</param>
            /// <param name="spinnerRadius">Spinner size (default: 24)</param>
            /// <param name="skeletonRowCount">Number of skeleton rows (default: 8)</param>
            /// <param name="skeletonBaseColor">Base color for skeleton bars (default: LightGray)</param>
            /// <param name="skeletonHighlightColor">Highlight/shimmer color for skeleton (default: WhiteSmoke)</param>
            public static void SetLoading(
                this DataGridView dgv,
                bool isLoading,
                string message = null,
                GridLoaderType loaderType = GridLoaderType.Spinner,
                Color? customColor = null,
                int? spinnerRadius = null,
                int? skeletonRowCount = null,
                Color? skeletonBaseColor = null,
                Color? skeletonHighlightColor = null)
            {
                if (dgv == null || dgv.IsDisposed) return;

                dgv.Paint -= OnGridPaint;
                dgv.Paint -= OnGridPaintSkeleton;

                if (!isLoading)
                {
                    GridLoaderManager.Remove(dgv);
                    return;
                }

                dgv.EnabledBuffer();

                var state = new GridLoaderState
                {
                    Message = message ?? "Loading...",
                    LoaderType = loaderType,
                    SpinnerRadius = spinnerRadius ?? 20,
                    SpinnerColor = customColor ?? Color.Gray,
                    SkeletonRowCount = skeletonRowCount ?? 8,
                    SkeletonBaseColor = skeletonBaseColor ?? Color.LightGray,
                    SkeletonHighlightColor = skeletonHighlightColor ?? Color.WhiteSmoke,
                };

                if (loaderType == GridLoaderType.Skeleton)
                {
                    state.SkeletonOffsets = new float[state.SkeletonRowCount];
                    for (int i = 0; i < state.SkeletonRowCount; i++)
                        state.SkeletonOffsets[i] = -100f * (i % 5);
                }

                GridLoaderManager.Add(dgv, state);

                dgv.Paint += loaderType == GridLoaderType.Skeleton ? OnGridPaintSkeleton : OnGridPaint;
            }

            // ── Private paint handlers ──────────────────────────────────────────────────────

            private static void OnGridPaint(object sender, PaintEventArgs e)
            {
                if (sender is not DataGridView dgv || dgv.Tag is not GridLoaderState state) return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                var rect = dgv.ClientRectangle;
                var center = new Point(rect.Width / 2, rect.Height / 2);

                if (state.LoaderType == GridLoaderType.Spinner && state.SpinnerRadius > 0)
                {
                    DrawSpinner(g, center, state.SpinnerRadius, GridLoaderManager.CurrentAngle, state.SpinnerColor);

                    if (!string.IsNullOrEmpty(state.Message))
                    {
                        using var brush = new SolidBrush(state.SpinnerColor);
                        var textSize = g.MeasureString(state.Message, dgv.Font);
                        var textPos = new PointF(
                            center.X - textSize.Width / 2,
                            center.Y + state.SpinnerRadius + 20);
                        g.DrawString(state.Message, dgv.Font, brush, textPos);
                    }
                }
                else if (!string.IsNullOrEmpty(state.Message))
                {
                    using var brush = new SolidBrush(Color.DimGray);
                    using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString(state.Message, dgv.Font, brush, rect, sf);
                }
            }

            private static void DrawSpinner(Graphics g, Point center, int radius, float angle, Color color)
            {
                int dotSize = SpinnerDotDiameter;
                int half = dotSize / 2;

                for (int i = 0; i < SpinnerDotCount; i++)
                {
                    float a = angle + i * (360f / SpinnerDotCount);
                    double rad = a * Math.PI / 180;
                    int x = center.X + (int)(Math.Cos(rad) * radius);
                    int y = center.Y + (int)(Math.Sin(rad) * radius);

                    int alpha = 255 - (int)(180f * i / SpinnerDotCount);
                    using var brush = new SolidBrush(Color.FromArgb(alpha, color));
                    g.FillEllipse(brush, x - half, y - half, dotSize, dotSize);
                }
            }

            private static void OnGridPaintSkeleton(object sender, PaintEventArgs e)
            {
                if (sender is not DataGridView dgv || dgv.Tag is not GridLoaderState state || state.SkeletonOffsets == null)
                    return;

                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.HighQuality;

                int headerHeight = dgv.ColumnHeadersHeight;
                int rowHeight = dgv.RowTemplate.Height;
                int cellHeight = rowHeight - SkeletonCellPadding * 2;
                int maxBarWidth = dgv.ClientSize.Width - 40;  // some padding

                // NEW: generate random widths once per row (if not already set)
                if (state.SkeletonWidths == null || state.SkeletonWidths.Length != state.SkeletonRowCount)
                {
                    state.SkeletonWidths = new float[state.SkeletonRowCount];
                    var rand = new Random();  // or use a static seeded one if you prefer reproducibility
                    for (int i = 0; i < state.SkeletonRowCount; i++)
                    {
                        // 60%–98% of max width → looks natural
                        state.SkeletonWidths[i] = (float)(maxBarWidth * (0.60 + rand.NextDouble() * 0.38));
                    }
                }

                using var gradient = new LinearGradientBrush(
                    new Rectangle(0, 0, maxBarWidth, cellHeight),
                    Color.FromArgb((int)(255 * SkeletonOpacity), state.SkeletonBaseColor),
                    Color.FromArgb((int)(255 * SkeletonOpacity), state.SkeletonHighlightColor),
                    0f);

                gradient.Blend = new Blend { Positions = [0f, 0.4f, 0.5f, 0.6f, 1f], Factors = [0f, 0f, 1f, 0f, 0f] };

                int y = headerHeight + SkeletonCellPadding;
                for (int i = 0; i < state.SkeletonRowCount && y + cellHeight < dgv.ClientSize.Height; i++)
                {
                    var rect = new Rectangle(10, y, (int)state.SkeletonWidths[i], cellHeight);
                    using var brush = (LinearGradientBrush)gradient.Clone();
                    brush.TranslateTransform(state.SkeletonOffsets[i], 0);
                    using var path = GetRoundedRectangle(rect, SkeletonCornerRadius);
                    g.FillPath(brush, path);
                    y += rowHeight;
                }
            }

            private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
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

