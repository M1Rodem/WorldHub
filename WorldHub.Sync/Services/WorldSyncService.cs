using System.IO.Compression;
using System.Text.Json;
using WorldHub.Infrastructure.Google;
using WorldHub.Logging;

namespace WorldHub.Sync.Services;

public sealed record ServerPackageManifest(
    string ServerName,
    string MinecraftVersion,
    string? Loader,
    string? LoaderVersion,
    DateTimeOffset PushedAtUtc,
    string PushedByUserName,
    string PushedByDeviceId,
    string ArchiveFileName,
    long ArchiveSizeBytes,
    string PackageHash);

public sealed record ServerPackageSyncStatus(
    bool HasCloudPackage,
    ServerPackageManifest? Manifest,
    string? CloudFileId);

public sealed class WorldSyncService
{
    private const string ManifestFileName = "manifest.json";
    private const string ServerSubfolderName = "server_package";

    // Файлы и папки, которые не нужно включать в облачный архив сервера
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "logs",
        "crash-reports",
        ".git",
        ".vs",
        "backups"
    };

    private static readonly HashSet<string> IgnoredFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "session.lock"
    };

    private readonly GoogleDriveClient _googleDriveClient;

    public WorldSyncService(GoogleDriveClient googleDriveClient)
    {
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        _googleDriveClient = googleDriveClient;
    }

    /// <summary>
    /// Проверяет наличие пакета сервера в общей папке WorldHub-сервера.
    /// </summary>
    public async Task<ServerPackageSyncStatus> CheckCloudServerStatusAsync(
        string worldHubFolderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldHubFolderId);

        try
        {
            var serverFolderId = await _googleDriveClient.GetOrCreateSubfolderAsync(
                worldHubFolderId,
                ServerSubfolderName,
                cancellationToken);

            var manifestFile = await _googleDriveClient.FindFileInFolderAsync(
                serverFolderId,
                ManifestFileName,
                cancellationToken);

            if (manifestFile is null)
            {
                return new ServerPackageSyncStatus(false, null, null);
            }

            // Скачиваем манифест во временную память
            var tempManifestPath = Path.Combine(Path.GetTempPath(), $"worldhub_server_manifest_{Guid.NewGuid():N}.json");
            try
            {
                await _googleDriveClient.DownloadFileAsync(
                    manifestFile.Id,
                    tempManifestPath,
                    manifestFile.Size,
                    null,
                    cancellationToken);

                var json = await File.ReadAllTextAsync(tempManifestPath, cancellationToken);
                var manifest = JsonSerializer.Deserialize<ServerPackageManifest>(json);

                var archiveFile = manifest is not null
                    ? await _googleDriveClient.FindFileInFolderAsync(serverFolderId, manifest.ArchiveFileName, cancellationToken)
                    : null;

                return new ServerPackageSyncStatus(
                    HasCloudPackage: archiveFile is not null,
                    Manifest: manifest,
                    CloudFileId: archiveFile?.Id);
            }
            finally
            {
                if (File.Exists(tempManifestPath))
                {
                    try { File.Delete(tempManifestPath); } catch { }
                }
            }
        }
        catch (Exception exception)
        {
            AppLog.Warning($"[SYNC] Ошибка проверки облачного пакета сервера: {exception.Message}", exception);
            return new ServerPackageSyncStatus(false, null, null);
        }
    }

    /// <summary>
    /// Выполняет Push: архивирует ВСЮ директорию Minecraft-сервера и выгружает её в Google Drive.
    /// </summary>
    public async Task PushServerAsync(
        string worldHubFolderId,
        string serverDirectory,
        string serverName,
        string minecraftVersion,
        string? loader,
        string? loaderVersion,
        string userName,
        string deviceId,
        IProgress<double>? progress = null,
        IProgress<string>? statusMessage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldHubFolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(serverName);

        if (!Directory.Exists(serverDirectory))
        {
            throw new DirectoryNotFoundException($"Папка сервера не найдена: {serverDirectory}");
        }

        statusMessage?.Report("Подготовка папки сервера в Google Drive...");
        var serverFolderId = await _googleDriveClient.GetOrCreateSubfolderAsync(
            worldHubFolderId,
            ServerSubfolderName,
            cancellationToken);

        var tempDir = Path.Combine(Path.GetTempPath(), "WorldHub_Server_Push_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var archiveFileName = "server.zip";
        var zipPath = Path.Combine(tempDir, archiveFileName);
        var manifestPath = Path.Combine(tempDir, ManifestFileName);

        try
        {
            statusMessage?.Report("Архивация всех файлов сервера...");
            AppLog.Log($"[SYNC] Начало архивации сервера из {serverDirectory}...");

            await Task.Run(() =>
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var dirInfo = new DirectoryInfo(serverDirectory);
                var rootLen = dirInfo.FullName.Length;
                if (!dirInfo.FullName.EndsWith(Path.DirectorySeparatorChar))
                {
                    rootLen++;
                }

                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var relativePath = file.FullName[rootLen..].Replace('\\', '/');

                    if (ShouldSkipFile(relativePath, file.Name))
                    {
                        continue;
                    }

                    archive.CreateEntryFromFile(file.FullName, relativePath, CompressionLevel.Fastest);
                }
            }, cancellationToken);

            var fileInfo = new FileInfo(zipPath);
            AppLog.Success($"[SYNC] Сервер упакован: {zipPath} ({fileInfo.Length / (1024 * 1024):N1} МБ)");

            statusMessage?.Report("Загрузка архива сервера в Google Drive...");
            var archiveProgress = new Progress<double>(p =>
            {
                progress?.Report(p * 0.9); // 0-90% на архив
            });

            var archiveFileId = await _googleDriveClient.UploadFileAsync(
                serverFolderId,
                zipPath,
                archiveFileName,
                "application/zip",
                archiveProgress,
                cancellationToken);

            statusMessage?.Report("Создание манифеста сервера...");
            var manifest = new ServerPackageManifest(
                ServerName: serverName,
                MinecraftVersion: minecraftVersion,
                Loader: loader,
                LoaderVersion: loaderVersion,
                PushedAtUtc: DateTimeOffset.UtcNow,
                PushedByUserName: userName,
                PushedByDeviceId: deviceId,
                ArchiveFileName: archiveFileName,
                ArchiveSizeBytes: fileInfo.Length,
                PackageHash: string.Empty);

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);

            statusMessage?.Report("Загрузка манифеста сервера...");
            await _googleDriveClient.UploadFileAsync(
                serverFolderId,
                manifestPath,
                ManifestFileName,
                "application/json",
                null,
                cancellationToken);

            progress?.Report(100.0);
            statusMessage?.Report("Синхронизация сервера (Push) успешно завершена!");
            AppLog.Success($"[SYNC] Push сервера '{serverName}' успешно выполнен!");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    /// <summary>
    /// Выполняет Pull: скачивает архив сервера из Google Drive и распаковывает его в targetDirectory.
    /// Перед распаковкой создаёт бэкап папки, если она уже существует.
    /// </summary>
    public async Task PullServerAsync(
        string worldHubFolderId,
        string targetDirectory,
        IProgress<double>? progress = null,
        IProgress<string>? statusMessage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldHubFolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        statusMessage?.Report("Проверка данных сервера в Google Drive...");
        var cloudStatus = await CheckCloudServerStatusAsync(worldHubFolderId, cancellationToken);

        if (!cloudStatus.HasCloudPackage || string.IsNullOrWhiteSpace(cloudStatus.CloudFileId))
        {
            throw new InvalidOperationException("В общей папке WorldHub нет сохранённой копии сервера. Сначала выполните Push с сервера-источника.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "WorldHub_Server_Pull_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, "server.zip");

        try
        {
            statusMessage?.Report("Скачивание сервера из Google Drive...");
            AppLog.Log($"[SYNC] Начало скачивания архива сервера ({cloudStatus.Manifest?.ArchiveSizeBytes ?? 0} байт)...");

            var downloadProgress = new Progress<double>(p =>
            {
                progress?.Report(p * 0.85); // 0-85% на скачивание
            });

            await _googleDriveClient.DownloadFileAsync(
                cloudStatus.CloudFileId,
                zipPath,
                cloudStatus.Manifest?.ArchiveSizeBytes,
                downloadProgress,
                cancellationToken);

            AppLog.Success($"[SYNC] Архив сервера скачан: {zipPath}");

            statusMessage?.Report("Резервное копирование текущей папки сервера...");
            if (Directory.Exists(targetDirectory) && Directory.EnumerateFileSystemEntries(targetDirectory).Any())
            {
                var parentDir = Directory.GetParent(targetDirectory)?.FullName ?? targetDirectory;
                var backupDirName = $"{Path.GetFileName(targetDirectory)}_backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                var backupPath = Path.Combine(parentDir, backupDirName);

                try
                {
                    AppLog.Log($"[SYNC] Резервная копия текущей папки сервера -> {backupPath}");
                    CopyDirectory(targetDirectory, backupPath);
                }
                catch (Exception ex)
                {
                    AppLog.Warning($"Не удалось создать локальную резервную копию: {ex.Message}", ex);
                }
            }

            statusMessage?.Report("Распаковка сервера в рабочую папку...");
            await Task.Run(() =>
            {
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                ZipFile.ExtractToDirectory(zipPath, targetDirectory, overwriteFiles: true);
            }, cancellationToken);

            progress?.Report(100.0);
            statusMessage?.Report("Сервер успешно скачан и установлен!");
            AppLog.Success($"[SYNC] Pull сервера завершён в {targetDirectory}!");
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private static bool ShouldSkipFile(string relativePath, string fileName)
    {
        if (IgnoredFiles.Contains(fileName))
        {
            return true;
        }

        var parts = relativePath.Split('/');
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (IgnoredDirectories.Contains(parts[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
        {
            return;
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            var nextTargetSubDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, nextTargetSubDir);
        }
    }
}
