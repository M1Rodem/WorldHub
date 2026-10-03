using System.IO;
using System.Windows;
using WorldHub.App.Services.Diagnostics;
using WorldHub.App.Services.Identity;
using WorldHub.App.Services.Network;
using WorldHub.App.Services.Settings;
using WorldHub.App.Views.Main;
using WorldHub.App.Services.Application;
using WorldHub.Sync.Hashing;
using WorldHub.Infrastructure.Storage;
using WorldHub.Network.Minecraft;
using WorldHub.Network.Providers;
using WorldHub.Network.Services;
using WorldHub.Sync.Services;
using WorldHub.Sync.Transfer;

namespace WorldHub.App;

public partial class App : Application
{
    private const int DefaultNetworkPort = 27072;
    private const int DefaultModPort = 27071;

    private MinecraftBridgeListener? _minecraftBridgeListener;
    private WorldHubNetworkService? _worldHubNetworkService;

    protected override void OnStartup(StartupEventArgs e)
    {
        DebugConsole.Initialize();
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            File.WriteAllText(
                "startup-error.txt",
                args.ExceptionObject.ToString());
        };
        DebugConsole.Log("WorldHub starting...");
        base.OnStartup(e);

        var networkPort = GetPortArgument(
            e.Args,
            "--network-port",
            DefaultNetworkPort);

        var modPort = GetPortArgument(
            e.Args,
            "--mod-port",
            DefaultModPort);

        DebugConsole.Log(
            $"WorldHub ports: network={networkPort}, mod={modPort}");

        var appSettingsService =
            new AppSettingsService();

        var dataPath =
            appSettingsService.GetDataPath();

        var worldsPath = Path.Combine(
            dataPath,
            "worlds");

        var snapshotsPath = Path.Combine(
            dataPath,
            "snapshots");

        var ownershipPath = Path.Combine(
            dataPath,
            "ownership");

        var worldRepository =
            new JsonWorldRepository(worldsPath);

        var snapshotRepository =
            new JsonSnapshotRepository(snapshotsPath);

        var snapshotStorage =
            new SnapshotFileStorage(snapshotsPath);

        var worldHashService =
            new WorldHashService();

        var ownershipRepository =
            new JsonWorldOwnershipRepository(ownershipPath);

        var worldService =
            new WorldService(worldRepository);

        var snapshotService =
            new SnapshotService(
                snapshotStorage,
                snapshotRepository,
                worldHashService,
                worldRepository);

        var worldDeletionService =
            new WorldDeletionService(
                worldRepository,
                ownershipRepository,
                snapshotService);

        var worldAppService =
            new WorldAppService(
                worldService,
                snapshotService);

        var snapshotAppService =
            new SnapshotAppService(snapshotService);

        var localPlayerIdentity =
            new LocalPlayerIdentity(
                Guid.Parse(
                    "11111111-1111-1111-1111-111111111111"));

        var networkProvider = new TcpNetworkProvider();

        var networkService =
            new NetworkService(networkProvider);

        var receivedWorldsPath =
            Path.Combine(
                dataPath,
                "received-worlds");

        var snapshotTransferService =
            new SnapshotTransferService(
                snapshotRepository,
                snapshotService,
                worldRepository);

        var worldTransferOrchestrator =
            new WorldTransferOrchestrator(
                networkService,
                snapshotRepository,
                worldService,
                snapshotService,
                snapshotTransferService,
                receivedWorldsPath,
                localPlayerIdentity.PlayerId);

        _worldHubNetworkService =
            new WorldHubNetworkService(
                networkService,
                worldTransferOrchestrator,
                networkPort);

        var mainWindow = new MainWindow(
            worldAppService,
            snapshotAppService,
            worldDeletionService,
            localPlayerIdentity,
            _worldHubNetworkService,
            worldTransferOrchestrator,
            dataPath);

        worldTransferOrchestrator.SetTransferConfirmationHandler(
            mainWindow.ShowIncomingTransferConfirmationAsync);

        _worldHubNetworkService.Start();

        mainWindow.Show();

        MainWindow = mainWindow;

        var worldHubSessionService =
            new WorldHubSessionService(
                worldAppService,
                snapshotAppService,
                localPlayerIdentity,
                mainWindow.RefreshDataAsync);

        _minecraftBridgeListener =
            new MinecraftBridgeListener(
                modPort,
                message => worldHubSessionService.HandleSessionEndedAsync(message),
                DebugConsole.Log,
                DebugConsole.Error);

        _minecraftBridgeListener.Start();

        DebugConsole.Log(
            "WorldHub TCP bridge started.");

        DebugConsole.Log(
            "WorldHub UI initialized.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DebugConsole.Log(
            "WorldHub shutting down...");

        _minecraftBridgeListener?.Dispose();
        _minecraftBridgeListener = null;

        _worldHubNetworkService?.Dispose();
        _worldHubNetworkService = null;

        DebugConsole.Close();

        base.OnExit(e);
    }

    private static int GetPortArgument(
        string[] arguments,
        string argumentName,
        int defaultPort)
    {
        var prefix = argumentName + "=";

        foreach (var argument in arguments)
        {
            if (!argument.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = argument[prefix.Length..];

            if (int.TryParse(value, out var port) &&
                port is >= 1 and <= 65535)
            {
                return port;
            }

            DebugConsole.Error(
                $"Invalid port argument: {argument}. " +
                $"Using default port {defaultPort}.");

            return defaultPort;
        }

        return defaultPort;
    }
}