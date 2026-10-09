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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object _lock = new();
    private readonly string _defaultDataPath;
    private string _dataPath;

    public AppSettingsService()
    {
        var localAppDataPath =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        _defaultDataPath =
            Path.Combine(
                localAppDataPath,
                "WorldHub",
                "data");

        _dataPath =
            LoadDataPathFromRegistry()
            ?? _defaultDataPath;

        Directory.CreateDirectory(_dataPath);
    }

    public string GetDataPath()
    {
        lock (_lock)
        {
            return _dataPath;
        }
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

        var fullPath = Path.GetFullPath(dataPath);

        lock (_lock)
        {
            Directory.CreateDirectory(fullPath);

            SaveDataPathToRegistry(fullPath);

            _dataPath = fullPath;

            SaveSettingsLocked(fullPath, settings =>
            {
                settings.DataPath = fullPath;
            });
        }
    }

    public string GetUserName()
    {
        lock (_lock)
        {
            var settings = LoadSettingsLocked(_dataPath);

            if (!string.IsNullOrWhiteSpace(settings.UserName))
            {
                return settings.UserName;
            }

            return Environment.UserName;
        }
    }

    public void SaveUserName(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new ArgumentException(
                "Имя пользователя не может быть пустым.",
                nameof(userName));
        }

        var trimmed = userName.Trim();

        lock (_lock)
        {
            SaveSettingsLocked(_dataPath, settings =>
            {
                settings.DataPath = _dataPath;
                settings.UserName = trimmed;
            });
        }
    }

    private static string? LoadDataPathFromRegistry()
    {
        try
        {
            using var key =
                Registry.CurrentUser.OpenSubKey(
                    RegistryKeyPath);

            var value =
                key?.GetValue(
                    RegistryDataPathValue) as string;

            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var fullPath =
                Path.GetFullPath(value);

            /*
             * Старый формат:
             * %LOCALAPPDATA%\WorldHub
             *
             * Новый формат:
             * %LOCALAPPDATA%\WorldHub\data
             */
            var localAppDataPath =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);

            var legacyRoot =
                Path.Combine(
                    localAppDataPath,
                    "WorldHub");

            if (string.Equals(
                    fullPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    legacyRoot.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                fullPath =
                    Path.Combine(
                        legacyRoot,
                        "data");

                SaveDataPathToRegistry(fullPath);
            }

            Directory.CreateDirectory(fullPath);

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

    private static AppSettings LoadSettingsLocked(string dataPath)
    {
        var settingsPath =
            Path.Combine(
                dataPath,
                SettingsFileName);

        if (!File.Exists(settingsPath))
        {
            return new AppSettings { DataPath = dataPath };
        }

        try
        {
            var json = File.ReadAllText(settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);

            if (settings is not null)
            {
                return settings;
            }
        }
        catch
        {
            // fallback below
        }

        return new AppSettings { DataPath = dataPath };
    }

    private static void SaveSettingsLocked(
        string dataPath,
        Action<AppSettings> updateAction)
    {
        var settingsPath =
            Path.Combine(
                dataPath,
                SettingsFileName);

        var settings = LoadSettingsLocked(dataPath);

        updateAction(settings);

        var json = JsonSerializer.Serialize(
            settings,
            JsonOptions);

        Directory.CreateDirectory(dataPath);

        var tempFilePath = $"{settingsPath}.tmp";

        try
        {
            File.WriteAllText(tempFilePath, json);

            File.Move(
                tempFilePath,
                settingsPath,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                }
            }
        }
    }

    private sealed class AppSettings
    {
        public string? DataPath { get; set; }

        public string? UserName { get; set; }
    }
}