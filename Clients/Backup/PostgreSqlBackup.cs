using Next.Clients.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Next.Clients.Backup
{
    internal static class PostgreSqlBackupStrategy
    {
        public static async Task<bool> DumpAsync(Base db, string filePath)
        {
            Logs.Write(db.Logger, "BACKUP", db.Name, $"PostgreSQL backup started → {filePath}");
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var tables = await db.QueryAsync<string>(
                "SELECT tablename FROM pg_tables WHERE schemaname = 'public' ORDER BY tablename");

            using var writer = new StreamWriter(filePath, false, new UTF8Encoding(false));
            await writer.WriteLineAsync($"-- PostgreSQL Full Backup: {db.Name} @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync("SET statement_timeout = 0;");

            foreach (var table in tables)
            {
                Logs.Write(db.Logger, "BACKUP", db.Name, $"Processing table: public.\"{table}\"");

                await writer.WriteLineAsync($"\n-- =========================================");
                await writer.WriteLineAsync($"-- Table: public.\"{table}\"");
                await writer.WriteLineAsync($"-- =========================================");

                var createScript = await GetCreateTableScript(db, table);
                await writer.WriteLineAsync(createScript);

                await DumpTableData(db, table, writer);
            }

            Logs.Write(db.Logger, "BACKUP", db.Name, $"PostgreSQL backup completed → {filePath}");
            return true;
        }

        private static async Task<string> GetCreateTableScript(Base db, string table)
        {
            try
            {
                var sql = @"
                    SELECT 'CREATE TABLE public.""' || @tbl || '"" (\n' ||
                           string_agg(
                             '  ""' || column_name || '"" ' || data_type ||
                             CASE WHEN character_maximum_length IS NOT NULL THEN '(' || character_maximum_length || ')'
                                  WHEN numeric_precision IS NOT NULL THEN '(' || numeric_precision ||
                                  CASE WHEN numeric_scale > 0 THEN ',' || numeric_scale ELSE '' END || ')'
                                  ELSE ''
                             END ||
                             CASE WHEN is_nullable = 'NO' THEN ' NOT NULL' ELSE '' END,
                             E',\n'
                           ) || E'\n);'
                    FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = @tbl
                    GROUP BY table_name";

                return await db.QueryFirstOrDefaultAsync<string>(sql, new { tbl = table }) ?? "-- CREATE TABLE script not generated\n";
            }
            catch (Exception ex)
            {
                Logs.Write(db.Logger, "WARN", db.Name, $"PG CREATE script failed for \"{table}\": {ex.Message}");
                return $"-- Failed: CREATE TABLE public.\"{table}\"\n";
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
                    var rows = await db.QueryAsync<dynamic>($"SELECT * FROM public.\"{table}\" LIMIT {batch} OFFSET {offset}");
                    if (!rows.Any()) break;

                    foreach (var row in rows)
                    {
                        var values = new List<string>();
                        foreach (var prop in row.GetType().GetProperties())
                        {
                            var val = prop.GetValue(row);
                            if (val == null || val is DBNull) values.Add("NULL");
                            else if (val is string || val is DateTime || val is Guid)
                                values.Add($"'{val.ToString()!.Replace("'", "''")}'");
                            else if (val is bool b) values.Add(b ? "true" : "false");
                            else if (val is byte[] bytes)
                                values.Add($"'\\x{BitConverter.ToString(bytes).Replace("-", "")}'");
                            else values.Add(val.ToString()!);
                        }
                        await writer.WriteLineAsync($"INSERT INTO public.\"{table}\" VALUES ({string.Join(", ", values)});");
                        total++;
                    }
                    offset += batch;
                }

                if (total > 0)
                    Logs.Write(db.Logger, "BACKUP", db.Name, $"Dumped {total} row(s) from public.\"{table}\"");
            }
            catch (Exception ex)
            {
                Logs.Write(db.Logger, "ERROR", db.Name, $"PG data dump failed for \"{table}\": {ex.Message}");
            }
        }
    }
}
