using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;

namespace WorldHub.Sync.Services;

public sealed class SessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IWorldRepository _worldRepository;
    private readonly IWorldOwnershipRepository _ownershipRepository;

    public SessionService(
        ISessionRepository sessionRepository,
        IWorldRepository worldRepository,
        IWorldOwnershipRepository ownershipRepository)
    {
        _sessionRepository = sessionRepository;
        _worldRepository = worldRepository;
        _ownershipRepository = ownershipRepository;
    }

    public async Task<Session> CreateAsync(
        Guid worldId,
        Guid hostPlayerId,
        long startSnapshotId,
        CancellationToken cancellationToken = default)
    {
        if (worldId == Guid.Empty)
        {
            throw new ArgumentException(
                "World ID cannot be empty.",
                nameof(worldId));
        }

        if (hostPlayerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Host player ID cannot be empty.",
                nameof(hostPlayerId));
        }

        if (startSnapshotId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startSnapshotId),
                "Start snapshot ID must be greater than zero.");
        }

        var world = await _worldRepository.GetByIdAsync(
            worldId,
            cancellationToken);

        if (world is null)
        {
            throw new InvalidOperationException(
                $"World '{worldId}' was not found.");
        }

        var activeSession =
            await _sessionRepository.GetActiveByWorldIdAsync(
                worldId,
                cancellationToken);

        if (activeSession is not null)
        {
            throw new InvalidOperationException(
                $"World already has active session '{activeSession.Id}'.");
        }

        var ownership =
            await _ownershipRepository.GetActiveAsync(
                worldId,
                cancellationToken);

        if (ownership is null)
        {
            throw new InvalidOperationException(
                "World must have an active owner before starting a session.");
        }

        if (ownership.PlayerId != hostPlayerId)
        {
            throw new InvalidOperationException(
                "Only the current world owner can start the session.");
        }

        if (ownership.SnapshotId != startSnapshotId)
        {
            throw new InvalidOperationException(
                "Session snapshot does not match the owned world snapshot.");
        }

        var session = new Session
        {
            Id = Guid.NewGuid(),
            WorldId = worldId,
            HostPlayerId = hostPlayerId,
            Status = SessionStatus.Created,
            StartedAt = DateTime.UtcNow,
            EndedAt = null,
            StartSnapshotId = startSnapshotId,
            ResultSnapshotId = null
        };

        await _sessionRepository.AddAsync(
            session,
            cancellationToken);

        world.Status = WorldStatus.Playing;
        world.UpdatedAt = DateTime.UtcNow;

        await _worldRepository.UpdateAsync(
            world,
            cancellationToken);

        return session;
    }

    public async Task<Session> SetStatusAsync(
        Guid sessionId,
        SessionStatus status,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException(
                $"Session '{sessionId}' was not found.");
        }

        session.Status = status;

        await _sessionRepository.UpdateAsync(
            session,
            cancellationToken);

        return session;
    }

    public async Task CompleteAsync(
        Guid sessionId,
        long resultSnapshotId,
        CancellationToken cancellationToken = default)
    {
        if (resultSnapshotId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resultSnapshotId),
                "Result snapshot ID must be greater than zero.");
        }

        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException(
                $"Session '{sessionId}' was not found.");
        }

        session.Status = SessionStatus.Completed;
        session.ResultSnapshotId = resultSnapshotId;
        session.EndedAt = DateTime.UtcNow;

        await _sessionRepository.UpdateAsync(
            session,
            cancellationToken);

        var world = await _worldRepository.GetByIdAsync(
            session.WorldId,
            cancellationToken);

        if (world is not null)
        {
            world.Status = WorldStatus.Ready;
            world.UpdatedAt = DateTime.UtcNow;

            await _worldRepository.UpdateAsync(
                world,
                cancellationToken);
        }
    }

    public async Task FailAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(
            sessionId,
            cancellationToken);

        if (session is null)
        {
            throw new InvalidOperationException(
                $"Session '{sessionId}' was not found.");
        }

        session.Status = SessionStatus.Failed;
        session.EndedAt = DateTime.UtcNow;

        await _sessionRepository.UpdateAsync(
            session,
            cancellationToken);

        var world = await _worldRepository.GetByIdAsync(
            session.WorldId,
            cancellationToken);

        if (world is not null)
        {
            world.Status = WorldStatus.Error;
            world.UpdatedAt = DateTime.UtcNow;

            await _worldRepository.UpdateAsync(
                world,
                cancellationToken);
        }
    }
}