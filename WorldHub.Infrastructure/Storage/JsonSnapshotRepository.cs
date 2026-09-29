using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonSnapshotRepository : ISnapshotRepository
{
    private readonly string _snapshotsRootPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonSnapshotRepository(string snapshotsRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            snapshotsRootPath);

        _snapshotsRootPath = snapshotsRootPath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(_snapshotsRootPath);
    }

    public async Task<Snapshot?> GetByIdAsync(
        long snapshotId,
        CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(
            _snapshotsRootPath,
            $"{snapshotId}.json",
            SearchOption.AllDirectories);

        var file = files.FirstOrDefault();

        if (file is null)
        {
            return null;
        }

        await using var stream = new FileStream(
            file,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<Snapshot>(
            stream,
            _jsonOptions,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Snapshot>> GetByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var worldDirectory = Path.Combine(
            _snapshotsRootPath,
            worldId.ToString());

        if (!Directory.Exists(worldDirectory))
        {
            return Array.Empty<Snapshot>();
        }

        var files = Directory.EnumerateFiles(
            worldDirectory,
            "*.json",
            SearchOption.TopDirectoryOnly);

        var snapshots = new List<Snapshot>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = new FileStream(
                file,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var snapshot = await JsonSerializer.DeserializeAsync<Snapshot>(
                stream,
                _jsonOptions,
                cancellationToken);

            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots
            .OrderBy(snapshot => snapshot.Version)
            .ToArray();
    }

    public async Task<Snapshot?> GetLatestByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var snapshots = await GetByWorldIdAsync(
            worldId,
            cancellationToken);

        return snapshots
            .OrderByDescending(snapshot => snapshot.Version)
            .FirstOrDefault();
    }

    public async Task<long> GetNextVersionAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var latestSnapshot = await GetLatestByWorldIdAsync(
            worldId,
            cancellationToken);

        return latestSnapshot is null
            ? 1
            : latestSnapshot.Version + 1;
    }

    public Task DeleteByWorldIdAsync(
    Guid worldId,
    CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.Combine(
            _snapshotsRootPath,
            worldId.ToString());

        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }

        return Task.CompletedTask;
    }

    public async Task AddAsync(
        Snapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var worldDirectory = Path.Combine(
            _snapshotsRootPath,
            snapshot.WorldId.ToString());

        Directory.CreateDirectory(worldDirectory);

        var filePath = Path.Combine(
            worldDirectory,
            $"{snapshot.Id}.json");

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Snapshot with ID '{snapshot.Id}' already exists.");
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            snapshot,
            _jsonOptions,
            cancellationToken);
    }

    public Task DeleteAsync(
        long snapshotId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = Directory.EnumerateFiles(
            _snapshotsRootPath,
            $"{snapshotId}.json",
            SearchOption.AllDirectories);

        foreach (var file in files)
        {
            File.Delete(file);
        }

        return Task.CompletedTask;
    }
}