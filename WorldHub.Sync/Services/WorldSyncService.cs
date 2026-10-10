using System.IO.Compression;
using System.Text.Json;
using WorldHub.Infrastructure.Google;
using WorldHub.Logging;

namespace WorldHub.Sync.Services;

public sealed record WorldManifest(
    string WorldName,
    string MinecraftVersion,
    string? Loader,
    string? LoaderVersion,
    DateTimeOffset PushedAtUtc,
    string PushedByUserName,
    string PushedByDeviceId,
    string ArchiveFileName,
    long ArchiveSizeBytes,
    string WorldHash);

public sealed record WorldSyncStatus(
    bool HasCloudWorld,
    WorldManifest? Manifest,
    string? CloudFileId);

public sealed class WorldSyncService
{
    private const string ManifestFileName = "manifest.json";
    private const string WorldsSubfolderName = "worlds";

    private readonly GoogleDriveClient _googleDriveClient;

    public WorldSyncService(GoogleDriveClient googleDriveClient)
    {
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        _googleDriveClient = googleDriveClient;
    }

    /// <summary>
    /// Проверяет наличие сохранённого мира в общей папке WorldHub-сервера.
    /// </summary>
    public async Task<WorldSyncStatus> CheckCloudWorldStatusAsync(
        string worldHubFolderId,
        string worldName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldHubFolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldName);

        try
        {
            var worldsFolderId = await _googleDriveClient.GetOrCreateSubfolderAsync(
                worldHubFolderId,
                WorldsSubfolderName,
                cancellationToken);

            var specificWorldFolderId = await _googleDriveClient.GetOrCreateSubfolderAsync(
                worldsFolderId,
                worldName,
                cancellationToken);

            var manifestFile = await _googleDriveClient.FindFileInFolderAsync(
                specificWorldFolderId,
                ManifestFileName,
                cancellationToken);

            if (manifestFile is null)
            {
                return new WorldSyncStatus(false, null, null);
            }

            // Скачиваем манифест во временную память
            var tempManifestPath = Path.Combine(Path.GetTempPath(), $"worldhub_manifest_{Guid.NewGuid():N}.json");
            try
            {
                await _googleDriveClient.DownloadFileAsync(
                    manifestFile.Id,
                    tempManifestPath,
                    manifestFile.Size,
                    null,
                    cancellationToken);

                var json = await File.ReadAllTextAsync(tempManifestPath, cancellationToken);
                var manifest = JsonSerializer.Deserialize<WorldManifest>(json);

                var archiveFile = manifest is not null
                    ? await _googleDriveClient.FindFileInFolderAsync(specificWorldFolderId, manifest.ArchiveFileName, cancellationToken)
                    : null;

                return new WorldSyncStatus(
                    HasCloudWorld: archiveFile is not null,
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
            AppLog.Warning($"[SYNC] Ошибка проверки облачного мира '{worldName}': {exception.Message}", exception);
            return new WorldSyncStatus(false, null, null);
        }
    }

    /// <summary>
    /// Выполняет Push: архивирует папку мира и загружает её вместе с manifest.json в Google Drive.
    /// </summary>
    public async Task PushWorldAsync(
        string worldHubFolderId,
        string localWorldPath,
        string worldName,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldName);

        if (!Directory.Exists(localWorldPath))
        {
            throw new DirectoryNotFoundException($"Папка мира не найдена: {localWorldPath}");
        }

        statusMessage?.Report("Подготовка папок в Google Drive...");
        var worldsFolderId = await _googleDriveClient.GetOrCreateSubfolderAsync(
            worldHubFolderId,
            WorldsSubfolderName,
            cancellationToken);

        var specificWorldFolderId = await _googleDriveClient.GetOrCreateSubfolderAsync(
            worldsFolderId,
            worldName,
            cancellationToken);

        var tempDir = Path.Combine(Path.GetTempPath(), "WorldHub_Push_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, $"{worldName}.zip");
        var manifestPath = Path.Combine(tempDir, ManifestFileName);

        try
        {
            statusMessage?.Report("Архивация мира...");
            AppLog.Log($"[SYNC] Начало архивации мира '{worldName}' из {localWorldPath}...");

            await Task.Run(() =>
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var dirInfo = new DirectoryInfo(localWorldPath);
                var rootLen = dirInfo.FullName.Length;
                if (!dirInfo.FullName.EndsWith(Path.DirectorySeparatorChar))
                {
                    rootLen++;
                }

                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Игнорируем session.lock
                    if (string.Equals(file.Name, "session.lock", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var entryName = file.FullName[rootLen..].Replace('\\', '/');
                    archive.CreateEntryFromFile(file.FullName, entryName, CompressionLevel.Fastest);
                }
            }, cancellationToken);

            var fileInfo = new FileInfo(zipPath);
            AppLog.Success($"[SYNC] Мир упакован: {zipPath} ({fileInfo.Length / (1024 * 1024):N1} МБ)");

            statusMessage?.Report("Загрузка архива в Google Drive...");
            var archiveProgress = new Progress<double>(p =>
            {
                progress?.Report(p * 0.9); // 0-90% на архив
            });

            var archiveFileId = await _googleDriveClient.UploadFileAsync(
                specificWorldFolderId,
                zipPath,
                $"{worldName}.zip",
                "application/zip",
                archiveProgress,
                cancellationToken);

            statusMessage?.Report("Создание манифеста мира...");
            var manifest = new WorldManifest(
                WorldName: worldName,
                MinecraftVersion: minecraftVersion,
                Loader: loader,
                LoaderVersion: loaderVersion,
                PushedAtUtc: DateTimeOffset.UtcNow,
                PushedByUserName: userName,
                PushedByDeviceId: deviceId,
                ArchiveFileName: $"{worldName}.zip",
                ArchiveSizeBytes: fileInfo.Length,
                WorldHash: string.Empty);

            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);

            statusMessage?.Report("Загрузка манифеста...");
            await _googleDriveClient.UploadFileAsync(
                specificWorldFolderId,
                manifestPath,
                ManifestFileName,
                "application/json",
                null,
                cancellationToken);

            progress?.Report(100.0);
            statusMessage?.Report("Синхронизация (Push) успешно завершена!");
            AppLog.Success($"[SYNC] Push мира '{worldName}' успешно выполнен!");
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
    /// Выполняет Pull: скачивает архив мира из Google Drive и распаковывает его в localWorldPath.
    /// Перед распаковкой создаёт резервную копию текущего мира (если он существует).
    /// </summary>
    public async Task PullWorldAsync(
        string worldHubFolderId,
        string localWorldPath,
        string worldName,
        IProgress<double>? progress = null,
        IProgress<string>? statusMessage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldHubFolderId);
        ArgumentException.ThrowIfNullOrWhiteSpace(localWorldPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldName);

        statusMessage?.Report("Проверка данных мира в облаке...");
        var cloudStatus = await CheckCloudWorldStatusAsync(worldHubFolderId, worldName, cancellationToken);

        if (!cloudStatus.HasCloudWorld || string.IsNullOrWhiteSpace(cloudStatus.CloudFileId))
        {
            throw new InvalidOperationException($"В общей папке WorldHub нет сохранённой копии мира «{worldName}». Сначала выполните Push с сервера-источника.");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "WorldHub_Pull_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var zipPath = Path.Combine(tempDir, $"{worldName}.zip");

        try
        {
            statusMessage?.Report("Скачивание архива мира из Google Drive...");
            AppLog.Log($"[SYNC] Начало скачивания архива мира '{worldName}' ({cloudStatus.Manifest?.ArchiveSizeBytes ?? 0} байт)...");

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

            AppLog.Success($"[SYNC] Архив успешно скачан: {zipPath}");

            statusMessage?.Report("Резервное копирование текущего мира...");
            if (Directory.Exists(localWorldPath))
            {
                var parentDir = Directory.GetParent(localWorldPath)?.FullName ?? localWorldPath;
                var backupDirName = $"{Path.GetFileName(localWorldPath)}_backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                var backupPath = Path.Combine(parentDir, backupDirName);

                try
                {
                    // Делаем быстрый бэкап переименованием или копированием
                    AppLog.Log($"[SYNC] Резервная копия существующего мира -> {backupPath}");
                    CopyDirectory(localWorldPath, backupPath);
                }
                catch (Exception ex)
                {
                    AppLog.Warning($"Не удалось создать локальную резервную копию мира: {ex.Message}", ex);
                }
            }

            statusMessage?.Report("Распаковка мира в папку сервера...");
            await Task.Run(() =>
            {
                if (!Directory.Exists(localWorldPath))
                {
                    Directory.CreateDirectory(localWorldPath);
                }

                // Распаковываем поверх с заменой
                ZipFile.ExtractToDirectory(zipPath, localWorldPath, overwriteFiles: true);
            }, cancellationToken);

            progress?.Report(100.0);
            statusMessage?.Report("Синхронизация (Pull) успешно завершена!");
            AppLog.Success($"[SYNC] Pull мира '{worldName}' завершён в {localWorldPath}!");
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
