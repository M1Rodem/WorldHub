using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonWorldHubServerRepository : IWorldHubServerRepository
{
    private readonly string _directoryPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public JsonWorldHubServerRepository(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        _directoryPath = directoryPath;

        Directory.CreateDirectory(_directoryPath);
    }

    public async Task<WorldHubServer?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(id);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            return await JsonSerializer.DeserializeAsync<WorldHubServer>(
                stream,
                JsonOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<WorldHubServer>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(
            _directoryPath,
            "*.json",
            SearchOption.TopDirectoryOnly);

        var result = new List<WorldHubServer>();

        foreach (var filePath in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                var worldHubServer =
                    await JsonSerializer.DeserializeAsync<WorldHubServer>(
                        stream,
                        JsonOptions,
                        cancellationToken);

                if (worldHubServer is not null)
                {
                    result.Add(worldHubServer);
                }
            }
            catch (JsonException)
            {
                // Пропускаем повреждённый файл, чтобы не блокировать остальные данные
            }
        }

        return result;
    }

    public async Task AddAsync(
        WorldHubServer worldHubServer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worldHubServer);

        var filePath = GetFilePath(worldHubServer.Id);

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"WorldHub-server с ID {worldHubServer.Id} уже существует.");
        }

        await WriteAsync(
            filePath,
            worldHubServer,
            cancellationToken);
    }

    public async Task UpdateAsync(
        WorldHubServer worldHubServer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worldHubServer);

        var filePath = GetFilePath(worldHubServer.Id);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "WorldHub-server не найден.",
                filePath);
        }

        await WriteAsync(
            filePath,
            worldHubServer,
            cancellationToken);
    }

    public Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetFilePath(id);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private string GetFilePath(Guid id)
    {
        return Path.Combine(
            _directoryPath,
            $"{id}.json");
    }

    private static async Task WriteAsync(
        string filePath,
        WorldHubServer worldHubServer,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryFilePath = $"{filePath}.tmp";

        await using (var stream = new FileStream(
            temporaryFilePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                worldHubServer,
                JsonOptions,
                cancellationToken);
        }

        File.Move(
            temporaryFilePath,
            filePath,
            overwrite: true);
    }
}