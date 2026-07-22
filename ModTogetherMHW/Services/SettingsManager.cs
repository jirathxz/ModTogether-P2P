using System;
using System.IO;
using System.Text.Json;
using ModTogetherMHW.Models;

namespace ModTogetherMHW.Services
{
    public class SettingsManager
    {
        private readonly string _settingsFile;
        public AppSettings Current { get; private set; }

        public event Action? OnSettingsChanged;

        public SettingsManager()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appData, "ModTogetherMHW");
            Directory.CreateDirectory(appFolder);
            
            _settingsFile = Path.Combine(appFolder, "settings.json");
            Current = Load();
        }

        private AppSettings Load()
        {
            if (File.Exists(_settingsFile))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFile);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    return new AppSettings();
                }
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFile, json);
                OnSettingsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"[Error] Could not save settings: {ex.Message}");
            }
        }
    }
}
