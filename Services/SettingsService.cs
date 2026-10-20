using System;
using System.IO;
using System.Text.Json;
using aura.Models;

namespace aura.Services;

public class SettingsService
{
    private static SettingsService? _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    private readonly string _configDirectory;
    private readonly string _configFilePath;
    private AppSettings? _cachedSettings;

    public SettingsService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrEmpty(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        _configDirectory = Path.Combine(appData, "lumen-aura");
        _configFilePath = Path.Combine(_configDirectory, "settings.json");
    }

    public AppSettings GetSettings()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (File.Exists(_configFilePath))
            {
                string json = File.ReadAllText(_configFilePath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json);
            }
        }
        catch
        {
            // Fallback to default
        }

        _cachedSettings ??= new AppSettings();
        _cachedSettings.CurrentLanIp = NetworkService.Instance.GetLocalIpAddress();
        return _cachedSettings;
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_configFilePath, json);
            _cachedSettings = settings;
        }
        catch
        {
            // Ignore write errors in restricted sandbox
        }
    }
}
