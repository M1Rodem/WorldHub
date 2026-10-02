using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Network.Models;
using WorldHub.Network.Protocol;
using WorldHub.Network.Services;
using WorldHub.Sync.Services;
using WorldHub.Core.Diagnostics;

namespace WorldHub.Sync.Transfer;

internal sealed class WorldTransferSender
{
    private const int BufferSize = 64 * 1024;

    private readonly NetworkService _networkService;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly WorldService _worldService;
    private readonly SnapshotTransferService _snapshotTransferService;

    public WorldTransferSender(
        NetworkService networkService,
        ISnapshotRepository snapshotRepository,
        WorldService worldService,
        SnapshotTransferService snapshotTransferService)
    {
        _networkService = networkService;
        _snapshotRepository = snapshotRepository;
        _worldService = worldService;
        _snapshotTransferService = snapshotTransferService;
    }

    // ─────────────────────────────────────────────────────────
    // Push (sender side)
    // ─────────────────────────────────────────────────────────

    public async Task PushAsync(
        World world,
        string host,
        int port,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ValidatePort(port);

        var plan = await _snapshotTransferService.PrepareForPushAsync(
            world, cancellationToken);

        await using var connection = await _networkService.ConnectAsync(
            host, port, cancellationToken);

        await WorldTransferProtocol.SendMessageAsync(
            connection, WorldTransferProtocol.PushRequest, cancellationToken);

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            new PushRequest(world.Id, plan.Items.Count),
            cancellationToken);

        long transferredBytes = 0;

        foreach (var item in plan.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await SendSnapshotAsync(
                connection,
                world,
                item,
                progress,
                transferredBytes,
                cancellationToken);

            transferredBytes += item.TotalBytes;
        }

        await WorldTransferProtocol.SendMessageAsync(
            connection, WorldTransferProtocol.TransferCompleted, cancellationToken);

        var response = await WorldTransferProtocol.ReceiveMessageAsync(
            connection, cancellationToken);

        if (response != WorldTransferProtocol.TransferAccepted)
        {
            throw new InvalidOperationException(
                $"Remote WorldHub rejected the transfer: {response}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // HandlePull (server-side sending)
    // ─────────────────────────────────────────────────────────

    public async Task HandlePullAsync(
        NetworkConnection connection,
        string worldName,
        CancellationToken cancellationToken)
    {
        var worlds = await _worldService.GetAllAsync(cancellationToken);

        var world = worlds.FirstOrDefault(w =>
            string.Equals(w.Name, worldName, StringComparison.OrdinalIgnoreCase));

        if (world is null || world.CurrentSnapshotId <= 0)
        {
            await WorldTransferProtocol.SendMessageAsync(
                connection, WorldTransferProtocol.TransferRejected, cancellationToken);
            return;
        }

        var snapshot = await _snapshotRepository.GetByIdAsync(
            world.CurrentSnapshotId, cancellationToken);

        if (snapshot is null || !Directory.Exists(snapshot.StoragePath))
        {
            await WorldTransferProtocol.SendMessageAsync(
                connection, WorldTransferProtocol.TransferRejected, cancellationToken);
            return;
        }

        var plan = await _snapshotTransferService.PrepareForPushAsync(
            world, cancellationToken);

        var singleItem = plan.Items.Last();

        await WorldTransferProtocol.SendMessageAsync(
            connection, WorldTransferProtocol.SnapshotMetadata, cancellationToken);

        await WorldTransferProtocol.SendJsonAsync(
            connection,
            BuildMetadata(world, snapshot, singleItem),
            cancellationToken);

        foreach (var file in singleItem.Files)
        {
            await WorldTransferProtocol.SendJsonAsync(
                connection,
                new SnapshotFileMetadata(file.RelativePath, file.Length),
                cancellationToken);

            await using var source = new FileStream(
                file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, useAsync: true);

            await WorldTransferProtocol.SendFileAsync(
                connection, source, file.Length,
                cancellationToken: cancellationToken);
        }

        await WorldTransferProtocol.SendMessageAsync(
            connection, WorldTransferProtocol.TransferCompleted, cancellationToken);

        var response = await WorldTransferProtocol.ReceiveMessageAsync(
            connection, cancellationToken);

        if (response != WorldTransferProtocol.TransferAccepted)
        {
            throw new InvalidOperationException(
                $"Pull receiver rejected the snapshot: {response}");
        }
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────

    private async Task SendSnapshotAsync(
        NetworkConnection connection,
        World world,
        SnapshotTransferItem item,
        IProgress<long>? progress,
        long alreadyTransferred,
        CancellationToken cancellationToken)
    {
        await WorldTransferProtocol.SendJsonAsync(
            connection,
            BuildMetadata(world, item.Snapshot, item),
            cancellationToken);

        long fileBytes = 0;

        foreach (var file in item.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WorldTransferProtocol.SendJsonAsync(
                connection,
                new SnapshotFileMetadata(file.RelativePath, file.Length),
                cancellationToken);

            await using var source = new FileStream(
                file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, useAsync: true);

            if (progress is not null)
            {
                var fileProgress = new Progress<long>(fileTransferred =>
                    progress.Report(alreadyTransferred + fileBytes + fileTransferred));

                await WorldTransferProtocol.SendFileAsync(
                    connection, source, file.Length, fileProgress, cancellationToken);
            }
            else
            {
                await WorldTransferProtocol.SendFileAsync(
                    connection, source, file.Length,
                    cancellationToken: cancellationToken);
            }

            fileBytes += file.Length;
        }

        progress?.Report(alreadyTransferred + fileBytes);
    }

    private static SnapshotTransferMetadata BuildMetadata(
        World world,
        Snapshot snapshot,
        SnapshotTransferItem item) =>
        new(
            world.Id,
            world.Name,
            world.MinecraftVersion,
            world.Loader,
            world.LoaderVersion,
            world.ModpackHash,
            snapshot.Id,
            snapshot.Version,
            snapshot.ParentSnapshotId,
            snapshot.AuthorId,
            snapshot.WorldHash,
            snapshot.Message,
            snapshot.CreatedAt,
            item.TotalBytes,
            item.Files.Count);

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }
    }
}