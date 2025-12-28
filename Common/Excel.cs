using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Next.Common
{
    /// <summary>
    /// FREE, Enterprise-Grade, Excel Import & Export
    /// • Never throws — always returns ExcelResult with IsSuccess + Message
    /// • Full IntelliSense documentation
    /// • Import → DataTable / List<T>
    /// • Export → Beautiful .xlsx with styling, password, multiple sheets
    /// • Works with EPPlus 6.2.10 + .NET Framework 4.8
    /// </summary>
    public static class Excel
    {
        [Obsolete]
        static Excel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>
        /// Import -------------------------------------------------------------------------------
        /// var dtResult = Excel.Import(@"C:\Data\Users.xlsx");
        /// var listResult = Excel.ImportList<User>(@"C:\Data\Users.xlsx");
        /// Async Import -------------------------------------------------------------------------
        /// var dtAsync = await Excel.ImportExcelToDataTableAsync(@"C:\Data\Users.xlsx");
        /// var listAsync = await Excel.ImportExcelToListAsync<User>(@"C:\Data\Users.xlsx");
        /// Export -------------------------------------------------------------------------------
        /// var exportDt = Excel.Export(dtResult.Data, @"C:\Export\Users.xlsx");
        /// var exportList = Excel.Export(listResult.Data, @"C:\Export\Users.xlsx");
        /// Multiple sheets ----------------------------------------------------------------------
        /// var multi = Excel.ExportMultiple(@"C:\Export\All.xlsx",
        ///  ("Users", listResult.Data),
        ///  ("Orders", orderList)
        ///  );
        ///  </summary>
    
        // ==================================== IMPORT ====================================

        /// <summary>
        /// Safely imports first sheet of an Excel file into a DataTable.
        /// </summary>
        /// <param name="filePath">Full path to .xlsx file</param>
        /// <param name="hasHeader">true = first row contains headers (default)</param>
        /// <returns>ExcelResult&lt;DataTable&gt;</returns>
        /// <example>
        /// var result = Excel.Import(@"C:\Data\Sales.xlsx");
        /// if (result.IsSuccess)
        ///     dataGridView1.DataSource = result.Data;
        /// else
        ///     MessageBox.Show(result.Message);
        /// </example>
        public static ExcelResult<DataTable> Import(string filePath, bool hasHeader = true)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return ExcelResult<DataTable>.Error($"File not found: {filePath}");

            try
            {
                var dt = new DataTable();
                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    var ws = package.Workbook.Worksheets.FirstOrDefault();
                    if (ws?.Dimension == null)
                        return ExcelResult<DataTable>.Error("Excel file is empty or corrupted");

                    // Headers
                    for (int c = 1; c <= ws.Dimension.End.Column; c++)
                    {
                        string header = hasHeader
                            ? (ws.Cells[1, c].GetValue<string>() ?? $"Col{c}")
                            : $"Col{c}";
                        dt.Columns.Add(header);
                    }

                    // Rows
                    int startRow = hasHeader ? 2 : 1;
                    for (int r = startRow; r <= ws.Dimension.End.Row; r++)
                    {
                        var row = dt.NewRow();
                        for (int c = 1; c <= ws.Dimension.End.Column; c++)
                            row[c - 1] = ws.Cells[r, c].Value ?? DBNull.Value;
                        dt.Rows.Add(row);
                    }
                }
                return ExcelResult<DataTable>.Ok(dt, $"Imported {dt.Rows.Count} rows");
            }
            catch (Exception ex)
            {
                return ExcelResult<DataTable>.Error($"Import failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports Excel into strongly-typed List&lt;T&gt;
        /// </summary>
        /// <example>
        /// var users = Excel.ImportList&lt;User&gt;(path);
        /// if (users.IsSuccess) { ... }
        /// </example>
        public static ExcelResult<List<T>> ImportList<T>(string filePath, bool hasHeader = true) where T : new()
        {
            var tableResult = Import(filePath, hasHeader);
            if (!tableResult.IsSuccess)
                return ExcelResult<List<T>>.Error(tableResult.Message);

            try
            {
                var list = new List<T>();
                var props = typeof(T).GetProperties();

                foreach (DataRow row in tableResult.Data.Rows)
                {
                    var item = new T();
                    foreach (var prop in props)
                    {
                        if (tableResult.Data.Columns.Contains(prop.Name) && row[prop.Name] != DBNull.Value)
                        {
                            try
                            {
                                var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                                var value = Convert.ChangeType(row[prop.Name], targetType);
                                prop.SetValue(item, value);
                            }
                            catch { /* Skip incompatible column */ }
                        }
                    }
                    list.Add(item);
                }
                return ExcelResult<List<T>>.Ok(list, $"Imported {list.Count} items");
            }
            catch (Exception ex)
            {
                return ExcelResult<List<T>>.Error($"ImportList failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Imports Excel file into DataTable (supports single sheet or all sheets merged)
        /// </summary>
        /// <param name="filePath">Full path to .xlsx file</param>
        /// <param name="sheetName">Optional: specific sheet name (null = first sheet)</param>
        /// <param name="hasHeader">true = first row is header</param>
        /// <param name="allSheets">true = merge all sheets into one DataTable</param>
        /// <returns>ExcelResult<DataTable></returns>
        /// 
        /// <example>
        /// var result = await DocumentService.ImportExcelToDataTableAsync(@"C:\data.xlsx");
        /// if (result.IsSuccess) dataGridView1.DataSource = result.Data;
        /// </example>
        public static async Task<ExcelResult<DataTable>> ImportExcelToDataTableAsync(
            string filePath,
            string sheetName = null,
            bool hasHeader = true,
            bool allSheets = false)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return ExcelResult<DataTable>.Error("File not found: " + filePath);

            try
            {
                var dt = new DataTable();

                using (var package = new ExcelPackage(new FileInfo(filePath)))
                {
                    ExcelWorksheet[] sheets;
                    if (sheetName != null)
                        sheets = new[] { package.Workbook.Worksheets[sheetName] };
                    else if (allSheets)
                        sheets = package.Workbook.Worksheets.ToArray();
                    else
                        sheets = new[] { package.Workbook.Worksheets.FirstOrDefault() };

                    if (sheets.Length == 0 || sheets[0] == null)
                        return ExcelResult<DataTable>.Error("No worksheets found");

                    bool columnsAdded = false;
                    int totalRows = 0;

                    foreach (var ws in sheets)
                    {
                        if (ws == null || ws.Dimension == null) continue;

                        if (!columnsAdded)
                        {
                            for (int c = 1; c <= ws.Dimension.End.Column; c++)
                            {
                                string header = hasHeader
                                    ? (ws.Cells[1, c].GetValue<string>() ?? "Col" + c)
                                    : "Col" + c;
                                if (!string.IsNullOrEmpty(header))
                                    header = header.Trim();
                                dt.Columns.Add(header);
                            }
                            columnsAdded = true;
                        }

                        int startRow = hasHeader ? 2 : 1;
                        for (int r = startRow; r <= ws.Dimension.End.Row; r++)
                        {
                            DataRow row = dt.NewRow();
                            for (int c = 1; c <= ws.Dimension.End.Column; c++)
                            {
                                row[c - 1] = ws.Cells[r, c].Value ?? DBNull.Value;
                            }
                            dt.Rows.Add(row);
                            totalRows++;
                        }
                    }

                    return ExcelResult<DataTable>.Ok(dt, "Imported " + totalRows + " rows");
                }
            }
            catch (Exception ex)
            {
                return ExcelResult<DataTable>.Error("Import failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Imports Excel into strongly-typed List<T>
        /// </summary>
        /// <example>
        /// var users = await DocumentService.ImportExcelToListAsync<User>(@"C:\users.xlsx");
        /// </example>
        public static async Task<ExcelResult<List<T>>> ImportExcelToListAsync<T>(
            string filePath,
            string sheetName = null,
            bool hasHeader = true,
            bool allSheets = false) where T : new()
        {
            var result = await ImportExcelToDataTableAsync(filePath, sheetName, hasHeader, allSheets);
            if (!result.IsSuccess)
                return ExcelResult<List<T>>.Error(result.Message);

            try
            {
                var list = new List<T>();
                var props = typeof(T).GetProperties();

                foreach (DataRow row in result.Data.Rows)
                {
                    var item = new T();
                    foreach (var prop in props)
                    {
                        if (result.Data.Columns.Contains(prop.Name))
                        {
                            object val = row[prop.Name];
                            if (val == DBNull.Value || val == null) continue;

                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            try
                            {
                                object converted = Convert.ChangeType(val, targetType);
                                prop.SetValue(item, converted);
                            }
                            catch { }
                        }
                    }
                    list.Add(item);
                }

                return ExcelResult<List<T>>.Ok(list, "Imported " + list.Count + " items");
            }
            catch (Exception ex)
            {
                return ExcelResult<List<T>>.Error("Mapping failed: " + ex.Message);
            }
        }

        // ==================================== EXPORT ====================================

        /// <summary>
        /// Exports DataTable to beautiful Excel file
        /// </summary>
        public static ExcelResult Export(DataTable data, string filePath, string sheetName = "Data", string password = null)
            => ExportDataTable(data, filePath, sheetName, password);

        /// <summary>
        /// Exports any List&lt;T&gt; or IEnumerable&lt;T&gt; to Excel
        /// </summary>
        public static ExcelResult Export<T>(IEnumerable<T> data, string filePath, string sheetName = "Data", string password = null)
            => ExportCollection(data, filePath, sheetName, password);

        /// <summary>
        /// Exports multiple datasets to multiple sheets in one file
        /// </summary>
        public static ExcelResult ExportMultiple(string filePath, params (string SheetName, object Data)[] sheets)
        {
            if (sheets == null || sheets.Length == 0)
                return ExcelResult.Error("No sheets to export");

            return ExportInternal(() =>
            {
                using (var p = new ExcelPackage())
                {
                    foreach (var s in sheets)
                    {
                        if (s.Data == null) continue;
                        var ws = p.Workbook.Worksheets.Add(s.SheetName);

                        if (s.Data is DataTable dt)
                            ws.Cells["A1"].LoadFromDataTable(dt, true);
                        else
                            ws.Cells["A1"].LoadFromCollection(s.Data as dynamic ?? s.Data, true);

                        ApplyStyle(ws);
                    }
                    SaveSafe(p, filePath, null);
                }
            }, filePath);
        }

        // Private helpers
        private static ExcelResult ExportDataTable(DataTable dt, string filePath, string sheetName, string password)
        {
            if (dt == null) return ExcelResult.Error("DataTable is null");
            return ExportInternal(() =>
            {
                using (var p = new ExcelPackage())
                {
                    var ws = p.Workbook.Worksheets.Add(sheetName);
                    ws.Cells["A1"].LoadFromDataTable(dt, true);
                    ApplyStyle(ws);
                    SaveSafe(p, filePath, password);
                }
            }, filePath);
        }

        private static ExcelResult ExportCollection<T>(IEnumerable<T> collection, string filePath, string sheetName, string password)
        {
            if (collection == null) return ExcelResult.Error("Collection is null");
            return ExportInternal(() =>
            {
                using (var p = new ExcelPackage())
                {
                    var ws = p.Workbook.Worksheets.Add(sheetName);
                    ws.Cells["A1"].LoadFromCollection(collection, true);
                    ApplyStyle(ws);
                    SaveSafe(p, filePath, password);
                }
            }, filePath);
        }

        private static ExcelResult ExportInternal(Action action, string filePath)
        {
            try
            {
                action();
                return ExcelResult.Ok($"Exported → {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                return ExcelResult.Error($"Export failed: {ex.Message}");
            }
        }

        private static void ApplyStyle(ExcelWorksheet ws)
        {
            if (ws.Dimension == null) return;
            using (var r = ws.Cells[1, 1, 1, ws.Dimension.End.Column])
            {
                r.Style.Font.Bold = true;
                r.Style.Font.Color.SetColor(Color.White);
                r.Style.Fill.PatternType = ExcelFillStyle.Solid;
                r.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(0, 102, 204));
            }
            ws.Cells.AutoFitColumns();
        }

        private static void SaveSafe(ExcelPackage p, string filePath, string password)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            if (!string.IsNullOrEmpty(password))
                p.Encryption.Password = password;
            p.SaveAs(new FileInfo(filePath));
        }
    }

    /// <summary>
    /// Safe result
    /// </summary>
    public class ExcelResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }

        protected ExcelResult(bool success, string message)
        {
            IsSuccess = success;
            Message = message;
        }

        public static ExcelResult Ok(string message = "Success") => new ExcelResult(true, message);
        public static ExcelResult Error(string message) => new ExcelResult(false, message);
    }

    public class ExcelResult<T> : ExcelResult
    {
        public T Data { get; private set; }

        private ExcelResult(bool success, string message, T data = default) : base(success, message)
        {
            Data = data;
        }

        public static ExcelResult<T> Ok(T data, string message = "Success")
            => new ExcelResult<T>(true, message, data);

        public new static ExcelResult<T> Error(string message)
            => new ExcelResult<T>(false, message);
    }
}
