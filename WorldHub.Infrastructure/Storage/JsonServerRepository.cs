using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Logging;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonServerRepository : IServerRepository
{
    private readonly string _serversRootPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonServerRepository(string serversRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serversRootPath);

        _serversRootPath = serversRootPath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(_serversRootPath);
    }

    public async Task<Server?> GetByIdAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetServerFilePath(serverId);

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

            return await JsonSerializer.DeserializeAsync<Server>(
                stream,
                _jsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            AppLog.Warning(
                $"[STORAGE] Failed to parse server configuration at '{filePath}': {exception.Message}",
                exception);
            return null;
        }
    }

    public async Task<IReadOnlyCollection<Server>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(
            _serversRootPath,
            "server.json",
            SearchOption.AllDirectories);

        var servers = new List<Server>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                var server = await JsonSerializer.DeserializeAsync<Server>(
                    stream,
                    _jsonOptions,
                    cancellationToken);

                if (server is not null)
                {
                    servers.Add(server);
                }
            }
            catch (JsonException exception)
            {
                AppLog.Warning(
                    $"[STORAGE] Skipped corrupted server configuration at '{file}': {exception.Message}",
                    exception);
            }
        }

        return servers
            .OrderBy(server => server.Name)
            .ToArray();
    }

    public async Task AddAsync(
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        var filePath = GetServerFilePath(server.Id);

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Server with ID '{server.Id}' already exists.");
        }

        await WriteAsync(
            filePath,
            server,
            cancellationToken);
    }

    public async Task UpdateAsync(
        Server server,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        var filePath = GetServerFilePath(server.Id);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Server with ID '{server.Id}' was not found.",
                filePath);
        }

        await WriteAsync(
            filePath,
            server,
            cancellationToken);
    }

    public Task DeleteAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = GetServerDirectory(serverId);

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
        Server server,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (directory is null)
        {
            throw new InvalidOperationException(
                "Server directory could not be determined.");
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
                server,
                _jsonOptions,
                cancellationToken);
        }

        File.Move(
            temporaryFilePath,
            filePath,
            overwrite: true);
    }

    private string GetServerDirectory(Guid serverId)
    {
        return Path.Combine(
            _serversRootPath,
            serverId.ToString());
    }

    private string GetServerFilePath(Guid serverId)
    {
        return Path.Combine(
            GetServerDirectory(serverId),
            "server.json");
    }
}