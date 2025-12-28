using Next.Clients.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Next.Clients.Backup
{
    internal static class MySqlBackup
    {
        public static async Task<bool> DumpAsync(Base db, string filePath)
        {
            Logs.Write(db.Logger, "BACKUP", db.Name, $"MySQL backup started → {filePath}");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var tables = await db.QueryAsync<string>(
                "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME");

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
            await writer.WriteLineAsync($"-- MySQL Full Backup: {db.Name} @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 0;");

            foreach (var table in tables)
            {
                Logs.Write(db.Logger, "BACKUP", db.Name, $"Processing table: `{table}`");

                await writer.WriteLineAsync($"\n-- =========================================");
                await writer.WriteLineAsync($"-- Table: `{table}`");
                await writer.WriteLineAsync($"-- =========================================");

                var createScript = await GetCreateTableScript(db, table);
                await writer.WriteLineAsync(createScript);

                await DumpTableData(db, table, writer);
            }

            await writer.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 1;");
            Logs.Write(db.Logger, "BACKUP", db.Name, $"MySQL backup completed → {filePath}");
            return true;
        }

        private static async Task<string> GetCreateTableScript(Base db, string table)
        {
            try
            {
                var result = await db.QueryFirstOrDefaultAsync<dynamic>($"SHOW CREATE TABLE `{table}`");
                return (result?["Create Table"] ?? "-- No create script").ToString() + ";";
            }
            catch (Exception ex)
            {
                Logs.Write(db.Logger, "WARN", db.Name, $"MySQL CREATE failed for `{table}`: {ex.Message}");
                return $"-- Failed: SHOW CREATE TABLE `{table}`\n";
            }
        }

        private static async Task DumpTableData(Base db, string table, StreamWriter writer)
        {
            try
            {
                const int batch = 500;
                long offset = 0;
                int total = 0;

                while (true)
                {
                    var rows = await db.QueryAsync<dynamic>($"SELECT * FROM `{table}` LIMIT {batch} OFFSET {offset}");
                    if (!rows.Any()) break;

                    foreach (var row in rows)
                    {
                        var values = new List<string>();
                        foreach (var prop in row.GetType().GetProperties())
                        {
                            var val = prop.GetValue(row);
                            if (val == null || val is DBNull) values.Add("NULL");
                            else if (val is string || val is DateTime || val is Guid || val is byte[])
                                values.Add($"'{val.ToString()!.Replace("'", "''")}'");
                            else if (val is bool b) values.Add(b ? "1" : "0");
                            else values.Add(val.ToString()!);
                        }
                        await writer.WriteLineAsync($"INSERT INTO `{table}` VALUES ({string.Join(", ", values)});");
                        total++;
                    }
                    offset += batch;
                }

                if (total > 0)
                    Logs.Write(db.Logger, "BACKUP", db.Name, $"Dumped {total} row(s) from `{table}`");
            }
            catch (Exception ex)
            {
                Logs.Write(db.Logger, "ERROR", db.Name, $"MySQL data dump failed for `{table}`: {ex.Message}");
            }
        }
    }
}
