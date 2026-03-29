using System;
using System.IO;
using System.Text.Json;

namespace UtLoader.Services
{
    // This class defines exactly what information we want to remember
    public class AppSettings
    {
        public string OutputPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        public bool IsMp3 { get; set; } = true;
        public bool IsMp4 { get; set; } = false;
        public bool IsNative { get; set; } = false;
    }

    public class SettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            // Save the settings.json file in the same folder as the application
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        }

        public AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // If the file is corrupted or locked, just fall back to standard defaults
            }

            return new AppSettings();
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                // WriteIndented makes the JSON file nicely formatted and easy to read
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // If we can't save (e.g., folder permissions issue), fail silently so the app doesn't crash
            }
        }
    }
}