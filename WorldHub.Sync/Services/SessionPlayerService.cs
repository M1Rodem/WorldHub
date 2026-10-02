using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;
using WorldHub.Core.Rules;

namespace WorldHub.Sync.Services;

public sealed class SessionPlayerService
{
    private readonly ISessionPlayerRepository _repository;
    private readonly ISessionRepository _sessionRepository;

    public SessionPlayerService(
        ISessionPlayerRepository repository,
        ISessionRepository sessionRepository)
    {
        _repository = repository;
        _sessionRepository = sessionRepository;
    }

    public Task<IReadOnlyCollection<SessionPlayer>> GetBySessionIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetBySessionIdAsync(
            sessionId,
            cancellationToken);
    }

    public async Task<SessionPlayer> AddHostAsync(
        Session session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var existingPlayers =
            await _repository.GetBySessionIdAsync(
                session.Id,
                cancellationToken);

        if (existingPlayers.Count > 0)
        {
            throw new InvalidOperationException(
                "Session already has registered players.");
        }

        var host = new SessionPlayer
        {
            SessionId = session.Id,
            PlayerId = session.HostPlayerId,
            Role = SessionPlayerRole.Host,
            ConnectedAt = DateTime.UtcNow,
            DisconnectedAt = null
        };

        await _repository.AddAsync(
            host,
            cancellationToken);

        return host;
    }

    public async Task<SessionPlayer> JoinAsync(
        Guid sessionId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Session ID cannot be empty.",
                nameof(sessionId));
        }

        if (playerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Player ID cannot be empty.",
                nameof(playerId));
        }

        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException(
                $"Session '{sessionId}' was not found.");
        }

        if (session.Status is
            SessionStatus.Completed or
            SessionStatus.Failed)
        {
            throw new InvalidOperationException(
                "Cannot join a completed or failed session.");
        }

        var existing = await _repository.GetAsync(
            sessionId,
            playerId,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.DisconnectedAt is not null)
            {
                existing.ConnectedAt = DateTime.UtcNow;
                existing.DisconnectedAt = null;

                await _repository.UpdateAsync(
                    existing,
                    cancellationToken);
            }

            return existing;
        }

        var players =
            await _repository.GetBySessionIdAsync(
                sessionId,
                cancellationToken);

        if (SessionMaxPlayersRule.IsFull(players.Count))
        {
            throw new InvalidOperationException(
                $"The MVP session supports a maximum of " +
                $"{SessionMaxPlayersRule.MaxPlayers} players.");
        }

        var player = new SessionPlayer
        {
            SessionId = sessionId,
            PlayerId = playerId,
            Role = SessionPlayerRole.Player,
            ConnectedAt = DateTime.UtcNow,
            DisconnectedAt = null
        };

        await _repository.AddAsync(
            player,
            cancellationToken);

        return player;
    }

    public async Task LeaveAsync(
        Guid sessionId,
        Guid playerId,
        CancellationToken cancellationToken = default)
    {
        var player = await _repository.GetAsync(
            sessionId,
            playerId,
            cancellationToken);

        if (player is null)
        {
            throw new InvalidOperationException(
                "Player is not registered in this session.");
        }

        if (player.DisconnectedAt is null)
        {
            player.DisconnectedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(
                player,
                cancellationToken);
        }
    }
}