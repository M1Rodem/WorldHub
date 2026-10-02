using WorldHub.App.Services.Application;
using WorldHub.App.Services.Diagnostics;
using WorldHub.App.Services.Identity;
using WorldHub.Core.Rules;
using WorldHub.Network.Minecraft;

namespace WorldHub.App.Services.Network;

public sealed class WorldHubSessionService
{
    private readonly WorldAppService _worldAppService;
    private readonly SnapshotAppService _snapshotAppService;
    private readonly LocalPlayerIdentity _localPlayerIdentity;
    private readonly Func<Task>? _dataChangedHandler;

    public WorldHubSessionService(
        WorldAppService worldAppService,
        SnapshotAppService snapshotAppService,
        LocalPlayerIdentity localPlayerIdentity,
        Func<Task>? dataChangedHandler = null)
    {
        ArgumentNullException.ThrowIfNull(worldAppService);
        ArgumentNullException.ThrowIfNull(snapshotAppService);
        ArgumentNullException.ThrowIfNull(localPlayerIdentity);

        _worldAppService = worldAppService;
        _snapshotAppService = snapshotAppService;
        _localPlayerIdentity = localPlayerIdentity;
        _dataChangedHandler = dataChangedHandler;
    }

    public async Task HandleSessionEndedAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (!SessionEndedMessage.TryParse(message, out var parsed) ||
            parsed is null)
        {
            DebugConsole.Log(
                $"Invalid SESSION_ENDED message: {message}");

            return;
        }

        DebugConsole.Log(
            $"Session ended: sessionId={parsed.SessionId}, " +
            $"worldName={parsed.WorldName}, worldPath={parsed.WorldPath}");

        var worlds = await _worldAppService.GetWorldsAsync(
            cancellationToken);

        var worldViewModel = worlds.FirstOrDefault(
            world => WorldPathRule.IsSame(world.LocalPath, parsed.WorldPath));

        if (worldViewModel is null)
        {
            DebugConsole.Log(
                $"World not found for path: {parsed.WorldPath}");

            return;
        }

        var world = await _worldAppService.GetWorldByIdAsync(
            worldViewModel.Id,
            cancellationToken);

        if (world is null)
        {
            DebugConsole.Log(
                $"World not found by ID: {worldViewModel.Id}");

            return;
        }

        var snapshot = await _snapshotAppService.CreateSnapshotIfChangedAsync(
            world,
            _localPlayerIdentity.PlayerId,
            message,
            cancellationToken);

        if (snapshot is null)
        {
            DebugConsole.Log(
                $"No changes detected for world: {world.Name}");

            return;
        }

        DebugConsole.Log(
            $"Snapshot created automatically: " +
            $"world={world.Name}, " +
            $"version={snapshot.Version}");

        if (_dataChangedHandler is not null)
        {
            await _dataChangedHandler();
        }
    }
}