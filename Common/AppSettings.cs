using Newtonsoft.Json;
using Next.Common;          // contains Results, IResult, IResult<T>, etc.
using System;
using System.IO;
using System.Windows.Forms;

namespace Next.Common
{
    public static class AppSettings
    {
        private static readonly string SettingsPath =
            Path.Combine(Application.StartupPath, "appsettings.json");

        /// <summary>
        /// Saves settings to JSON file.
        /// </summary>
        public static IResult Save<T>(T settings)
        {
            try
            {
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsPath, json);
                return Results.Ok("Settings saved successfully.");
            }
            catch (Exception ex)
            {
                return Results.Error($"Failed to save settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads settings from JSON file. Returns new instance if missing or corrupted.
        /// </summary>
        public static IResult<T> Load<T>() where T : new()
        {
            if (!File.Exists(SettingsPath))
            {
                return Results.Ok<T>(new T(), "Settings file not found, returning default.");
            }

            try
            {
                string json = File.ReadAllText(SettingsPath);
                var result = JsonConvert.DeserializeObject<T>(json);

                // Handle null result from deserialization (very rare with Newtonsoft)
                var loaded = result ?? new T();

                return Results.Ok(loaded, "Settings loaded successfully.");
            }
            catch (Exception ex)
            {
                // Corrupted file → try to backup and reset
                try
                {
                    if (File.Exists(SettingsPath))
                    {
                        string backupPath = SettingsPath + $".corrupted.{DateTime.Now:yyyyMMdd-HHmmss}";
                        File.Move(SettingsPath, backupPath);
                    }
                }
                catch
                {
                    // ignore backup failure – still return default
                }

                return Results.Ok<T>(
                    new T(),
                    $"Settings file was corrupted and reset. Error: {ex.Message}"
                );
            }
        }
    }
}