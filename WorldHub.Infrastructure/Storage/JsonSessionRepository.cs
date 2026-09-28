using System.Text.Json;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;

namespace WorldHub.Infrastructure.Storage;

public sealed class JsonSessionRepository : ISessionRepository
{
    private readonly string _sessionsRootPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonSessionRepository(string sessionsRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionsRootPath);

        _sessionsRootPath = sessionsRootPath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        Directory.CreateDirectory(_sessionsRootPath);
    }

    public async Task<Session?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetSessionFilePath(sessionId);

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

        return await JsonSerializer.DeserializeAsync<Session>(
            stream,
            _jsonOptions,
            cancellationToken);
    }

    public async Task<Session?> GetActiveByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var sessions = await GetByWorldIdAsync(
            worldId,
            cancellationToken);

        return sessions
            .Where(session =>
                session.Status != SessionStatus.Completed &&
                session.Status != SessionStatus.Failed)
            .OrderByDescending(session => session.StartedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyCollection<Session>> GetByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default)
    {
        var files = Directory.EnumerateFiles(
            _sessionsRootPath,
            "*.json",
            SearchOption.TopDirectoryOnly);

        var sessions = new List<Session>();

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

            var session = await JsonSerializer.DeserializeAsync<Session>(
                stream,
                _jsonOptions,
                cancellationToken);

            if (session is not null && session.WorldId == worldId)
            {
                sessions.Add(session);
            }
        }

        return sessions
            .OrderBy(session => session.StartedAt)
            .ToArray();
    }

    public async Task AddAsync(
        Session session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var filePath = GetSessionFilePath(session.Id);

        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"Session '{session.Id}' already exists.");
        }

        await WriteAsync(
            filePath,
            session,
            cancellationToken);
    }

    public async Task UpdateAsync(
        Session session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var filePath = GetSessionFilePath(session.Id);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Session '{session.Id}' was not found.",
                filePath);
        }

        await WriteAsync(
            filePath,
            session,
            cancellationToken);
    }

    private async Task WriteAsync(
        string filePath,
        Session session,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_sessionsRootPath);

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
                session,
                _jsonOptions,
                cancellationToken);
        }

        File.Move(
            temporaryFilePath,
            filePath,
            overwrite: true);
    }

    private string GetSessionFilePath(Guid sessionId)
    {
        return Path.Combine(
            _sessionsRootPath,
            $"{sessionId}.json");
    }
}