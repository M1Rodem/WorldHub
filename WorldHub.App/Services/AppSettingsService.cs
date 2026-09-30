using System.IO;
using System.Text.Json;

namespace WorldHub.App.Services;

public sealed class AppSettingsService
{
    private const string SettingsDirectoryName = "WorldHub";
    private const string SettingsFileName = "settings.json";

    private readonly string _settingsPath;

    public AppSettingsService()
    {
        var localAppDataPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var settingsDirectory =
            Path.Combine(
                localAppDataPath,
                SettingsDirectoryName);

        Directory.CreateDirectory(settingsDirectory);

        _settingsPath =
            Path.Combine(
                settingsDirectory,
                SettingsFileName);
    }

    public string GetDataPath()
    {
        var settings = Load();

        if (!string.IsNullOrWhiteSpace(settings.DataPath))
        {
            return settings.DataPath;
        }

        return GetDefaultDataPath();
    }

    public string GetDefaultDataPath()
    {
        var localAppDataPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localAppDataPath,
            SettingsDirectoryName,
            "data");
    }

    public void SaveDataPath(string dataPath)
    {
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            throw new ArgumentException(
                "Data path cannot be empty.",
                nameof(dataPath));
        }

        var fullPath =
            Path.GetFullPath(dataPath);

        var settings = new AppSettings
        {
            DataPath = fullPath
        };

        Save(settings);
    }

    private AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json =
                File.ReadAllText(_settingsPath);

            return JsonSerializer.Deserialize<AppSettings>(
                       json,
                       new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       })
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    private void Save(AppSettings settings)
    {
        var json =
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            _settingsPath,
            json);
    }

    private sealed class AppSettings
    {
        public string? DataPath { get; init; }
    }
}