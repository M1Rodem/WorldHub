using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonWorldRepository : IWorldRepository
{
    private readonly string _worldsRootPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonWorldRepository(string worldsRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldsRootPath);

        _worldsRootPath = worldsRootPath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(_worldsRootPath);
    }

    public async Task<World?> GetByIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetWorldFilePath(worldId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonSerializer.DeserializeAsync<World>(
            stream,
            _jsonOptions,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<World>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(
            _worldsRootPath,
            "world.json",
            SearchOption.AllDirectories);

        var worlds = new List<World>();

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

            var world = await JsonSerializer.DeserializeAsync<World>(
                stream,
                _jsonOptions,
                cancellationToken);

            if (world is not null)
            {
                worlds.Add(world);
            }
        }

        return worlds
            .OrderBy(world => world.Name)
            .ToArray();
    }

    public async Task AddAsync(
        World world,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);

        var filePath = GetWorldFilePath(world.Id);

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"World with ID '{world.Id}' already exists.");
        }

        await WriteAsync(
            filePath,
            world,
            cancellationToken);
    }

    public async Task UpdateAsync(
        World world,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);

        var filePath = GetWorldFilePath(world.Id);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"World with ID '{world.Id}' was not found.",
                filePath);
        }

        await WriteAsync(
            filePath,
            world,
            cancellationToken);
    }

    public Task DeleteAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = GetWorldDirectory(worldId);

        if (Directory.Exists(directory))
        {
            Directory.Delete(
                directory,
                recursive: true);
        }

        return Task.CompletedTask;
    }

    private async Task WriteAsync(
        string filePath,
        World world,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (directory is null)
        {
            throw new InvalidOperationException(
                "World directory could not be determined.");
        }

        Directory.CreateDirectory(directory);

        var temporaryFilePath = $"{filePath}.tmp";

        await using (
            var stream = new FileStream(
                temporaryFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                world,
                _jsonOptions,
                cancellationToken);
        }

        File.Move(
            temporaryFilePath,
            filePath,
            overwrite: true);
    }

    private string GetWorldDirectory(Guid worldId)
    {
        return Path.Combine(
            _worldsRootPath,
            worldId.ToString());
    }

    private string GetWorldFilePath(Guid worldId)
    {
        return Path.Combine(
            GetWorldDirectory(worldId),
            "world.json");
    }
}