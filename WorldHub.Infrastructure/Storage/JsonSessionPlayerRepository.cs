using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonSessionPlayerRepository
    : ISessionPlayerRepository
{
    private readonly string _sessionPlayersRootPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonSessionPlayerRepository(
        string sessionPlayersRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sessionPlayersRootPath);

        _sessionPlayersRootPath = sessionPlayersRootPath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(_sessionPlayersRootPath);
    }

    public async Task<SessionPlayer?> GetAsync(
        Guid sessionId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(
            sessionId,
            playerId);

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

        return await JsonSerializer.DeserializeAsync<SessionPlayer>(
            stream,
            _jsonOptions,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<SessionPlayer>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var sessionDirectory = GetSessionDirectory(sessionId);

        if (!Directory.Exists(sessionDirectory))
        {
            return Array.Empty<SessionPlayer>();
        }

        var files = Directory.EnumerateFiles(
            sessionDirectory,
            "*.json",
            SearchOption.TopDirectoryOnly);

        var result = new List<SessionPlayer>();

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

            var sessionPlayer =
                await JsonSerializer.DeserializeAsync<SessionPlayer>(
                    stream,
                    _jsonOptions,
                    cancellationToken);

            if (sessionPlayer is not null)
            {
                result.Add(sessionPlayer);
            }
        }

        return result;
    }

    public async Task AddAsync(
        SessionPlayer sessionPlayer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionPlayer);

        var sessionDirectory =
            GetSessionDirectory(sessionPlayer.SessionId);

        Directory.CreateDirectory(sessionDirectory);

        var filePath = GetFilePath(
            sessionPlayer.SessionId,
            sessionPlayer.PlayerId);

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                "Player is already registered in this session.");
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
            sessionPlayer,
            _jsonOptions,
            cancellationToken);
    }

    public async Task UpdateAsync(
        SessionPlayer sessionPlayer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionPlayer);

        var filePath = GetFilePath(
            sessionPlayer.SessionId,
            sessionPlayer.PlayerId);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Session player record was not found.",
                filePath);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            sessionPlayer,
            _jsonOptions,
            cancellationToken);
    }

    private string GetSessionDirectory(Guid sessionId)
    {
        return Path.Combine(
            _sessionPlayersRootPath,
            sessionId.ToString());
    }

    private string GetFilePath(
        Guid sessionId,
        Guid playerId)
    {
        return Path.Combine(
            GetSessionDirectory(sessionId),
            $"{playerId}.json");
    }
}