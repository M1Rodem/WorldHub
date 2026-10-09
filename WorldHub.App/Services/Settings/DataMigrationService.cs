using System.IO;

namespace WorldHub.App.Services.Settings;

public sealed class DataMigrationService
{
    private readonly AppSettingsService _appSettingsService;

    public DataMigrationService(AppSettingsService appSettingsService)
    {
        ArgumentNullException.ThrowIfNull(appSettingsService);
        _appSettingsService = appSettingsService;
    }

    public void MigrateData(string currentDataPath, string newDataPath, string currentRootPath, string newRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRootPath);

        var normalizedCurrent = Path.GetFullPath(currentDataPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalizedNew = Path.GetFullPath(newDataPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalizedCurrentRoot = Path.GetFullPath(currentRootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var normalizedNewRoot = Path.GetFullPath(newRootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

        if (string.Equals(normalizedCurrent, normalizedNew, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (normalizedNewRoot.StartsWith(normalizedCurrentRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedCurrentRoot.StartsWith(normalizedNewRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedNew.StartsWith(normalizedCurrent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedCurrent.StartsWith(normalizedNew + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Целевой путь не может находиться внутри исходного пути или содержать его.");
        }

        if (Directory.Exists(normalizedNew) && Directory.EnumerateFileSystemEntries(normalizedNew).Any())
        {
            throw new InvalidOperationException(
                "Целевая папка данных уже существует и не пуста.");
        }

        var createdNewRoot = !Directory.Exists(normalizedNewRoot);
        Directory.CreateDirectory(normalizedNewRoot);
        Directory.CreateDirectory(normalizedNew);

        try
        {
            // Сначала полностью копируем всё содержимое в новую директорию
            CopyDirectoryContents(normalizedCurrent, normalizedNew);

            // Сохраняем новый путь в настройках и реестре
            _appSettingsService.SaveDataPath(normalizedNew);

            // Только после успешного копирования и сохранения настроек очищаем старую папку
            if (Directory.Exists(normalizedCurrentRoot))
            {
                try
                {
                    Directory.Delete(normalizedCurrentRoot, recursive: true);
                }
                catch
                {
                    // Если старая папка заблокирована каким-то процессом,
                    // новые данные уже скопированы и сохранены, это не приводит к потере данных.
                }
            }
        }
        catch
        {
            // Откат: если копирование прервалось или возникла ошибка,
            // очищаем созданную новую папку, сохраняя старые данные нетронутыми.
            try
            {
                if (Directory.Exists(normalizedNew))
                {
                    Directory.Delete(normalizedNew, recursive: true);
                }

                if (createdNewRoot && Directory.Exists(normalizedNewRoot) && !Directory.EnumerateFileSystemEntries(normalizedNewRoot).Any())
                {
                    Directory.Delete(normalizedNewRoot, recursive: false);
                }
            }
            catch
            {
                // ignore cleanup errors
            }

            throw;
        }
    }

    private static void CopyDirectoryContents(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var dir in Directory.EnumerateDirectories(sourceDirectory))
        {
            var dirName = Path.GetFileName(dir);
            var destSubDir = Path.Combine(destinationDirectory, dirName);
            CopyDirectoryContents(dir, destSubDir);
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destinationDirectory, fileName);
            File.Copy(file, destFile, overwrite: true);
        }
    }
}
