using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace WorldHub.App.Services.Settings;

public sealed class AppSettingsService
{
    private const string RegistryKeyPath =
        @"Software\WorldHub";

    private const string RegistryDataPathValue =
        "DataPath";

    private const string SettingsFileName =
        "settings.json";

    private readonly string _defaultDataPath;
    private string _dataPath;
    private string _settingsRootPath;

    public AppSettingsService()
    {
        var localAppDataPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        var defaultRootPath =
            Path.Combine(
                localAppDataPath,
                "WorldHub");

        _defaultDataPath =
            Path.Combine(
                defaultRootPath,
                "data");

        _settingsRootPath =
            LoadDataPathFromRegistry()
            ?? defaultRootPath;

        _dataPath =
            Path.Combine(
                _settingsRootPath,
                "data");
    }

    public string GetDataPath()
    {
        return _dataPath;
    }

    public string GetDefaultDataPath()
    {
        return _defaultDataPath;
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

        var dataPathRoot =
            Path.Combine(
                fullPath,
                "data");

        Directory.CreateDirectory(
            dataPathRoot);

        SaveDataPathToRegistry(
            fullPath);

        _settingsRootPath = fullPath;
        _dataPath = dataPathRoot;

        SaveSettingsFile(
            fullPath);
    }

    public void DeleteLegacySettingsDirectory()
    {
        var legacyDirectory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "WorldHub");

        if (!Directory.Exists(legacyDirectory))
        {
            return;
        }

        Directory.Delete(
            legacyDirectory,
            recursive: true);
    }

    private string? LoadDataPathFromRegistry()
    {
        try
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(
                    RegistryKeyPath);

            var value =
                key?.GetValue(
                    RegistryDataPathValue)
                as string;

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var fullPath =
                Path.GetFullPath(value);

            if (!Directory.Exists(fullPath))
            {
                return null;
            }

            return fullPath;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveDataPathToRegistry(
        string dataPath)
    {
        using var key =
            Registry.CurrentUser.CreateSubKey(
                RegistryKeyPath);

        if (key is null)
        {
            throw new InvalidOperationException(
                "Не удалось открыть настройки WorldHub в реестре.");
        }

        key.SetValue(
            RegistryDataPathValue,
            dataPath,
            RegistryValueKind.String);
    }

    private static void SaveSettingsFile(
        string dataPath)
    {
        var settingsPath =
            Path.Combine(
                dataPath,
                SettingsFileName);

        var settings =
            new AppSettings
            {
                DataPath = dataPath
            };

        var json =
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

        File.WriteAllText(
            settingsPath,
            json);
    }

    private sealed class AppSettings
    {
        public string? DataPath { get; init; }
    }
}