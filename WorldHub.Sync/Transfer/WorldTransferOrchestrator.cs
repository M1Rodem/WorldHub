using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Network.Models;
using WorldHub.Network.Protocol;
using WorldHub.Network.Services;
using WorldHub.Sync.Services;

namespace WorldHub.Sync.Transfer;

/// <summary>
/// Фасад над sender/receiver сервисами трансфера.
/// Используется UI и сервером для push/pull/handle-incoming.
/// </summary>
public sealed class WorldTransferOrchestrator
{
    private readonly WorldTransferSender _sender;
    private readonly WorldTransferReceiver _receiver;
    private Func<string, int, Task<bool>>? _transferConfirmationHandler;

    public WorldTransferOrchestrator(
        NetworkService networkService,
        ISnapshotRepository snapshotRepository,
        WorldService worldService,
        SnapshotService snapshotService,
        SnapshotTransferService snapshotTransferService,
        string receivedWorldsRootPath,
        Guid localPlayerId)
    {
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(snapshotRepository);
        ArgumentNullException.ThrowIfNull(worldService);
        ArgumentNullException.ThrowIfNull(snapshotService);
        ArgumentNullException.ThrowIfNull(snapshotTransferService);
        ArgumentException.ThrowIfNullOrWhiteSpace(receivedWorldsRootPath);

        if (localPlayerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Local player ID cannot be empty.", nameof(localPlayerId));
        }

        _sender = new WorldTransferSender(
            networkService, snapshotRepository, worldService, snapshotTransferService);

        _receiver = new WorldTransferReceiver(
            networkService,
            worldService,
            snapshotService,
            snapshotTransferService,
            receivedWorldsRootPath,
            localPlayerId,
            async (worldName, snapshotCount) =>
            {
                if (_transferConfirmationHandler is null)
                {
                    return false;
                }

                return await _transferConfirmationHandler(
                    worldName,
                    snapshotCount);
            });
    }

    // Публичный API — тот же, что был раньше.
    public Task PushAsync(
        World world, string host, int port,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
        => _sender.PushAsync(world, host, port, progress, cancellationToken);

    public Task PullAsync(
        World world, string host, int port,
        string? targetPath = null,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
        => _receiver.PullAsync(world, host, port, targetPath, progress, cancellationToken);

    public void SetTransferConfirmationHandler(
        Func<string, int, Task<bool>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        _transferConfirmationHandler = handler;
    }

    public async Task HandleIncomingAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var command = await WorldTransferProtocol.ReceiveMessageAsync(
            connection, cancellationToken);

        switch (command)
        {
            case WorldTransferProtocol.PushRequest:
                await _receiver.HandlePushAsync(connection, cancellationToken);
                return;

            case WorldTransferProtocol.PullRequest:
                await HandlePullRequestAsync(connection, cancellationToken);
                return;

            default:
                await WorldTransferProtocol.SendMessageAsync(
                    connection, WorldTransferProtocol.TransferRejected, cancellationToken);

                throw new InvalidDataException(
                    $"Unsupported transfer command: '{command}'.");
        }
    }

    private async Task HandlePullRequestAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken)
    {
        var request = await WorldTransferProtocol
            .ReceiveJsonAsync<PullRequest>(connection, cancellationToken);

        await _sender.HandlePullAsync(connection, request.WorldName, cancellationToken);
    }
}