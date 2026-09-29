using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonWorldOwnershipRepository
    : IWorldOwnershipRepository
{
    private readonly string _ownershipRootPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonWorldOwnershipRepository(
        string ownershipRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            ownershipRootPath);

        _ownershipRootPath = ownershipRootPath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(_ownershipRootPath);
    }

    public async Task<WorldOwnership?> GetActiveAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var history = await GetHistoryAsync(
            worldId,
            cancellationToken);

        return history
            .Where(x => x.Status == OwnershipStatus.Active)
            .OrderByDescending(x => x.AcquiredAt)
            .FirstOrDefault();
    }

    public async Task AddAsync(
        WorldOwnership ownership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownership);

        var directory = GetWorldDirectory(
            ownership.WorldId);

        Directory.CreateDirectory(directory);

        var filePath = Path.Combine(
            directory,
            $"{ownership.AcquiredAt.Ticks}.json");

        await using var stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            ownership,
            _jsonOptions,
            cancellationToken);
    }

    public async Task UpdateAsync(
        WorldOwnership ownership,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownership);

        var history = await GetHistoryAsync(
            ownership.WorldId,
            cancellationToken);

        var existing = history
            .FirstOrDefault(x =>
                x.AcquiredAt == ownership.AcquiredAt &&
                x.PlayerId == ownership.PlayerId);

        if (existing is null)
        {
            throw new InvalidOperationException(
                "Ownership record was not found.");
        }

        var directory = GetWorldDirectory(
            ownership.WorldId);

        var filePath = Path.Combine(
            directory,
            $"{ownership.AcquiredAt.Ticks}.json");

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            ownership,
            _jsonOptions,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<WorldOwnership>> GetHistoryAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var directory = GetWorldDirectory(worldId);

        if (!Directory.Exists(directory))
        {
            return Array.Empty<WorldOwnership>();
        }

        var files = Directory.EnumerateFiles(
            directory,
            "*.json",
            SearchOption.TopDirectoryOnly);

        var result = new List<WorldOwnership>();

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

            var ownership =
                await JsonSerializer.DeserializeAsync<WorldOwnership>(
                    stream,
                    _jsonOptions,
                    cancellationToken);

            if (ownership is not null)
            {
                result.Add(ownership);
            }
        }

        return result
            .OrderBy(x => x.AcquiredAt)
            .ToArray();
    }

    public Task DeleteByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = GetWorldDirectory(worldId);

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }

        return Task.CompletedTask;
    }

    private string GetWorldDirectory(Guid worldId)
    {
        return Path.Combine(
            _ownershipRootPath,
            worldId.ToString());
    }
}