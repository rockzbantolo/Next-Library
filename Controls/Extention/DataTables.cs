using Newtonsoft.Json;
using Next.Common;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Next.Controls.Extention
{
    /// <summary>
    /// Provides extension methods for materializing dynamic query results
    /// (typically from Dapper) into DataTable instances.
    ///
    /// PURPOSE
    /// - Normalize flexible query results into a deterministic tabular structure
    /// - Act as a boundary between data-access and WinForms UI
    /// - Eliminate dynamic / reflection usage inside UI code
    ///
    /// DESIGN NOTES
    /// - Assumes Dapper-style results (ExpandoObject → IDictionary<string, object>)
    /// - Uses object-typed columns for maximum WinForms stability
    /// - Safe for empty or null result sets
    /// </summary>
    public static class DataTables
    {
        /// <summary>
        /// Exports visible DataTable rows to JSON and passes to a save function.
        /// Hidden/internal columns can be excluded.
        /// Sync
        /// dt.ExportJson(json => Ops.InsertData(json), "Id");
        /// Async
        /// await dt.SaveTo(async json => await Ops.InsertDataAsync(json), "Id");
        /// </summary>
        /// <summary>
        /// Serializes a DataTable to JSON and executes a save action.
        /// Returns a safe Return indicating success or failure.
        /// </summary>
        /// <param name="table">Source DataTable</param>
        /// <param name="saveAction">Action to receive the JSON string</param>
        /// <param name="excludeColumns">Columns to exclude from serialization</param>
        /// <returns>Return indicating success/failure</returns>
        public static Return SaveJsonTo(this DataTable table, Action<string> saveAction, params string[] excludeColumns)
        {
            if (table == null)
                return Return.Error("DataTable is null.");

            if (saveAction == null)
                return Return.Error("Save action is null.");

            try
            {
                var colsToSerialize = table.Columns.Cast<DataColumn>()
                    .Where(c => excludeColumns == null || !excludeColumns.Contains(c.ColumnName))
                    .Select(c => c.ColumnName)
                    .ToArray();

                var list = table.AsEnumerable()
                    .Select(r => colsToSerialize.ToDictionary(col => col, col => r[col]))
                    .ToList();

                var json = JsonConvert.SerializeObject(list, Formatting.Indented);

                saveAction(json);

                return Return.Ok("Operation Succesfull");
            }
            catch (Exception ex)
            {
                return Return.Error($"Operation Failed: Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts an IEnumerable&lt;dynamic&gt; (Dapper result set) into a DataTable.
        ///
        /// COLUMN RULES
        /// - Columns are inferred from the first row's keys
        /// - All columns use typeof(object) for maximum compatibility
        ///
        /// SAFETY
        /// - Returns an empty DataTable if rows is null or empty
        /// - Null values are converted to DBNull.Value
        ///
        /// INTENDED USE
        /// - WinForms DataGridView binding
        /// - UI-facing service/controller layers
        ///
        /// NOT INTENDED FOR
        /// - Core data-access libraries
        /// - Business/domain logic
        /// </summary>
        /// <param name="rows">Dynamic query results (typically from Dapper)</param>
        /// <param name="tableName">Optional DataTable name (default: "Result")</param>
        public static DataTable ToDataTable(this IEnumerable<dynamic> rows,
            string tableName = "Result")
        {
            var dt = new DataTable(tableName);

            if (rows == null)
                return dt;

            var list = rows as IList<dynamic> ?? rows.ToList();
            if (list.Count == 0)
                return dt;

            // Dapper returns ExpandoObject → IDictionary<string, object>
            var firstRow = (IDictionary<string, object>)list[0];

            // Build schema
            foreach (var key in firstRow.Keys)
            {
                dt.Columns.Add(key, typeof(object));
            }

            // Populate rows
            foreach (var row in list)
            {
                var dr = dt.NewRow();
                var dict = (IDictionary<string, object>)row;

                foreach (var kvp in dict)
                {
                    dr[kvp.Key] = kvp.Value ?? DBNull.Value;
                }

                dt.Rows.Add(dr);
            }

            return dt;
        }

        /// <summary>
        /// Converts an IEnumerable&lt;IDictionary&lt;string, object&gt;&gt; into a DataTable.
        ///
        /// This overload exists for explicit dictionary-based pipelines or
        /// non-dynamic callers.
        /// </summary>
        /// <param name="rows">Dictionary-based row data</param>
        /// <param name="tableName">Optional DataTable name (default: "Result")</param>
        public static DataTable ToDataTable(this IEnumerable<IDictionary<string,
            object>> rows,
            string tableName = "Result")
        {
            var dt = new DataTable(tableName);

            if (rows == null)
                return dt;

            var list = rows as IList<IDictionary<string, object>> ?? rows.ToList();
            if (list.Count == 0)
                return dt;

            var firstRow = list[0];

            foreach (var key in firstRow.Keys)
            {
                dt.Columns.Add(key, typeof(object));
            }

            foreach (var row in list)
            {
                var dr = dt.NewRow();
                foreach (var kvp in row)
                {
                    dr[kvp.Key] = kvp.Value ?? DBNull.Value;
                }
                dt.Rows.Add(dr);
            }

            return dt;
        }

        /// <summary>
        /// Exports a DataTable to an Excel (.xlsx) file using EPPlus.
        /// Opens a SaveFileDialog and returns a safe result.
        /// </summary>
        /// <param name="table">Source DataTable</param>
        /// <param name="defaultFileName">Suggested default filename (e.g., "Export.xlsx")</param>
        /// <param name="sheetName">Worksheet name (default: "Sheet1")</param>
        /// <param name="includeHeaders">Include column headers</param>
        /// <param name="autoFit">Auto-fit column widths</param>
        /// <returns>Result indicating success or failure.</returns>
        public static IResult ToExcel(this DataTable table,
            string defaultFileName = "Data.xlsx",
            string sheetName = "Details",
            bool includeHeaders = true,
            bool autoFit = true)
        {
            try
            {
                if (table == null)
                    return Results.Error("DataTable is null.");

                using var sfd = new SaveFileDialog
                {
                    Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                    FileName = defaultFileName,
                    DefaultExt = "xlsx",
                    AddExtension = true,
                    Title = "Save as Excel File"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                    return Results.Error("Export cancelled by user.");

                ExcelPackage.License.SetNonCommercialPersonal("Next");

                using var package = new ExcelPackage();
                var ws = package.Workbook.Worksheets.Add(sheetName);

                int rowIndex = 1;
                int colCount = table.Columns.Count;

                // Headers
                if (includeHeaders)
                {
                    for (int c = 0; c < colCount; c++)
                    {
                        ws.Cells[rowIndex, c + 1].Value = table.Columns[c].ColumnName;
                        ws.Cells[rowIndex, c + 1].Style.Font.Bold = true;
                    }
                    rowIndex++;
                }

                // Data
                foreach (DataRow row in table.Rows)
                {
                    for (int c = 0; c < colCount; c++)
                        ws.Cells[rowIndex, c + 1].Value = row[c] == DBNull.Value ? null : row[c];
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
        /// Exports a DataTable to CSV using a SaveFileDialog.
        /// </summary>
        public static IResult ToCsv(this DataTable dt, string defaultFileName = "Data.csv")
        {
            if (dt == null)
                return Results.Error("DataTable is null. Nothing to export.");

            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = defaultFileName,
                    DefaultExt = "csv",
                    AddExtension = true,
                    Title = "Save as CSV File"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                    return Results.Error("CSV export canceled by user.");

                using var sw = new StreamWriter(sfd.FileName);

                // Write header
                sw.WriteLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => $"\"{c.ColumnName}\"")));

                // Write rows
                foreach (DataRow row in dt.Rows)
                    sw.WriteLine(string.Join(",", row.ItemArray.Select(v => $"\"{v}\"")));

                return Results.Ok("DataTable successfully exported to CSV.");
            }
            catch (Exception ex)
            {
                return Results.Error($"CSV export failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports a CSV file into a DataTable.
        /// </summary>
        public static IResult<DataTable> FromCsv()
        {
            try
            {
                using var ofd = new OpenFileDialog
                {
                    Filter = "CSV (*.csv)|*.csv",
                    Title = "Select CSV File"
                };

                if (ofd.ShowDialog() != DialogResult.OK)
                    return Results.Error<DataTable>("CSV import canceled by user.");

                var lines = File.ReadAllLines(ofd.FileName);
                if (lines.Length == 0)
                    return Results.Error<DataTable>("CSV file is empty.");

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

                return Results.Ok(dt, "CSV imported successfully.");
            }
            catch (Exception ex)
            {
                return Results.Error<DataTable>($"Failed to import CSV: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports a DataTable to JSON using a SaveFileDialog.
        /// </summary>
        public static IResult ToJson(this DataTable dt, string defaultFileName = "Data.json", params string[] excludeColumns)
        {
            if (dt == null)
                return Results.Error("DataTable is null.");

            try
            {
                using var sfd = new SaveFileDialog
                {
                    Filter = "JSON (*.json)|*.json",
                    FileName = defaultFileName,
                    DefaultExt = "json",
                    AddExtension = true,
                    Title = "Save as JSON File"
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                    return Results.Error("JSON export canceled by user.");

                var colsToSerialize = dt.Columns.Cast<DataColumn>()
                    .Where(c => excludeColumns == null || !excludeColumns.Contains(c.ColumnName))
                    .Select(c => c.ColumnName)
                    .ToArray();

                var list = dt.AsEnumerable()
                    .Select(r => colsToSerialize.ToDictionary(col => col, col => r[col]))
                    .ToList();

                var json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(sfd.FileName, json);

                return Results.Ok($"DataTable exported to JSON ({Path.GetFileName(sfd.FileName)}) successfully.");
            }
            catch (Exception ex)
            {
                return Results.Error($"Failed to export JSON: {ex.Message}");
            }
        }



        public static object GetValue(this DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName) ? row[columnName] : null;
        }

        public static T GetValue<T>(this DataRow row, string columnName)
        {
            if (!row.Table.Columns.Contains(columnName) || row[columnName] == DBNull.Value)
                return default;
            return (T)Convert.ChangeType(row[columnName], typeof(T));
        }

        public static DataTable Filter(this DataTable dt, string filterExpression)
        {
            var dv = new DataView(dt) { RowFilter = filterExpression };
            return dv.ToTable();
        }

        public static DataTable Sort(this DataTable dt, string sortExpression)
        {
            var dv = new DataView(dt) { Sort = sortExpression };
            return dv.ToTable();
        }

        public static DataTable CloneSchema(this DataTable dt, string newTableName = null)
        {
            var clone = dt.Clone();
            if (!string.IsNullOrEmpty(newTableName)) clone.TableName = newTableName;
            return clone;
        }

        public static void Append(this DataTable target, DataTable source)
        {
            foreach (DataRow row in source.Rows)
                target.ImportRow(row);
        }

        public static List<Dictionary<string, object>> ToDictionaryList(this DataTable dt)
        {
            return dt.AsEnumerable()
                     .Select(r => dt.Columns.Cast<DataColumn>()
                                             .ToDictionary(c => c.ColumnName, c => r[c]))
                     .ToList();
        }

        /// <summary>
        /// Adds a new row to the DataTable using a dictionary of column values.
        /// </summary>
        public static void AddRow(this DataTable table, params (string Column, object Value)[] values)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var row = table.NewRow();
            foreach (var (Column, Value) in values)
            {
                if (table.Columns.Contains(Column))
                    row[Column] = Value ?? DBNull.Value;
            }
            table.Rows.Add(row);
        }

        /// <summary>
        /// Copies a row from source table to target table (optionally specific columns).
        /// </summary>
        public static void CopyRowTo(this DataRow sourceRow, DataTable target, params string[] columns)
        {
            if (sourceRow == null || target == null) return;

            // If no columns specified, copy all
            var cols = columns.Length > 0 ? columns : sourceRow.Table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray();

            // Ensure target has columns
            foreach (var colName in cols)
            {
                if (!target.Columns.Contains(colName))
                    target.Columns.Add(colName, sourceRow.Table.Columns[colName].DataType);
            }

            var newRow = target.NewRow();
            foreach (var colName in cols)
            {
                newRow[colName] = sourceRow[colName];
            }

            target.Rows.Add(newRow);
        }

        /// <summary>
        /// Moves a row from source table to target table (copy + delete).
        /// </summary>
        public static void MoveRowTo(this DataRow sourceRow, DataTable target, params string[] columns)
        {
            sourceRow.CopyRowTo(target, columns);
            sourceRow.Delete();
        }

        /// <summary>
        /// Removes selected rows by index from a DataTable.
        /// </summary>
        public static void RemoveRows(this DataTable table, params int[] rowIndexes)
        {
            if (table == null) return;

            foreach (var index in rowIndexes.OrderByDescending(i => i))
            {
                if (index >= 0 && index < table.Rows.Count)
                    table.Rows[index].Delete();
            }
            table.AcceptChanges();
        }

        public static DataTable GetPage(this DataTable table, int pageIndex, int pageSize)
        {
            var dt = table.Clone();
            var rows = table.AsEnumerable().Skip(pageIndex * pageSize).Take(pageSize);
            foreach (var row in rows) dt.ImportRow(row);
            return dt;
        }

        public static void ReplaceNulls(this DataTable table, string columnName, object defaultValue)
        {
            if (!table.Columns.Contains(columnName)) return;
            foreach (DataRow row in table.Rows)
                if (row[columnName] == DBNull.Value)
                    row[columnName] = defaultValue;
        }

        public static void MoveRow(this DataTable table, int rowIndex, int newIndex)
        {
            if (rowIndex < 0 || rowIndex >= table.Rows.Count) return;
            if (newIndex < 0 || newIndex >= table.Rows.Count) return;

            var row = table.Rows[rowIndex];
            table.Rows.RemoveAt(rowIndex);
            table.Rows.InsertAt(row, newIndex);
        }

        public static void TrimStrings(this DataTable table)
        {
            foreach (DataColumn col in table.Columns)
            {
                if (col.DataType == typeof(string))
                    foreach (DataRow row in table.Rows)
                        row[col.ColumnName] = (row[col.ColumnName]?.ToString() ?? "").Trim();
            }
        }

        public static decimal Sum(this DataTable table, string columnName)
        {
            if (!table.Columns.Contains(columnName)) return 0;
            return table.AsEnumerable().Sum(r => Convert.ToDecimal(r[columnName] ?? 0));
        }

        public static decimal Average(this DataTable table, string columnName)
        {
            if (!table.Columns.Contains(columnName)) return 0;
            return table.AsEnumerable().Average(r => Convert.ToDecimal(r[columnName] ?? 0));
        }

        public static object Max(this DataTable table, string columnName)
        {
            return table.Columns.Contains(columnName) ? table.AsEnumerable().Max(r => r[columnName]) : null;
        }

        public static object Min(this DataTable table, string columnName)
        {
            return table.Columns.Contains(columnName) ? table.AsEnumerable().Min(r => r[columnName]) : null;
        }

        public static int Count(this DataTable table, string filterExpression = null)
        {
            return string.IsNullOrEmpty(filterExpression)
                ? table.Rows.Count
                : table.Select(filterExpression).Length;
        }

        public static DataTable GroupBySum(this DataTable table, string groupColumn, string sumColumn)
        {
            var grouped = table.AsEnumerable()
                .GroupBy(r => r[groupColumn])
                .Select(g =>
                {
                    var row = table.NewRow();
                    row[groupColumn] = g.Key;
                    row[sumColumn] = g.Sum(r => Convert.ToDecimal(r[sumColumn] ?? 0));
                    return row;
                });

            var result = new DataTable();
            result.Columns.Add(groupColumn, table.Columns[groupColumn].DataType);
            result.Columns.Add(sumColumn, typeof(decimal));

            foreach (var row in grouped)
                result.ImportRow(row);

            return result;
        }

        public static void AddComputedColumn(this DataTable table, string columnName, Func<DataRow, object> compute)
        {
            if (!table.Columns.Contains(columnName))
                table.Columns.Add(columnName, typeof(object));

            foreach (DataRow row in table.Rows)
                row[columnName] = compute(row);
        }

        public static DataTable TakeTop(this DataTable table, int n)
        {
            var dt = table.Clone();
            foreach (DataRow row in table.AsEnumerable().Take(n))
                dt.ImportRow(row);
            return dt;
        }

        public static decimal GetDecimal(this DataRow row, string columnName)
        {
            return row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value
                ? Convert.ToDecimal(row[columnName])
                : 0m;
        }

    }
}
