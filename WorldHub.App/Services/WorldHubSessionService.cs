using System.IO;

namespace WorldHub.App.Services;

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
        const string prefix = "SESSION_ENDED|";

        if (!message.StartsWith(prefix, StringComparison.Ordinal))
        {
            DebugConsole.Log(
                $"Invalid SESSION_ENDED message: {message}");

            return;
        }

        var parts = message.Split('|', 4);

        if (parts.Length != 4)
        {
            DebugConsole.Log(
                $"Invalid SESSION_ENDED message format: {message}");

            return;
        }

        var sessionId = parts[1];
        var worldName = parts[2];
        var worldPath = parts[3];

        DebugConsole.Log(
            $"Session ended: sessionId={sessionId}, " +
            $"worldName={worldName}, worldPath={worldPath}");

        var worlds = await _worldAppService.GetWorldsAsync(
            cancellationToken);

        var worldViewModel = worlds.FirstOrDefault(
            world => PathsEqual(world.LocalPath, worldPath));

        if (worldViewModel is null)
        {
            DebugConsole.Log(
                $"World not found for path: {worldPath}");

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

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            var normalizedLeft = Path.GetFullPath(left)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            var normalizedRight = Path.GetFullPath(right)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            return string.Equals(
                normalizedLeft,
                normalizedRight,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}