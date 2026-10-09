using System.IO;
using System.Windows;
using WorldHub.App.Services.Network;
using WorldHub.App.Services.Settings;
using WorldHub.App.Services.Update;
using WorldHub.App.Views.Main;
using WorldHub.Infrastructure.Google;
using WorldHub.Infrastructure.Minecraft;
using WorldHub.Infrastructure.Storage;
using WorldHub.Network.Protocol;
using WorldHub.Network.Providers;
using WorldHub.Network.Services;
using WorldHub.Sync.Services;

namespace WorldHub.App;

public partial class App : Application
{
    private WorldHubNetworkService? _worldHubNetworkService;

    protected override void OnStartup(
        StartupEventArgs e)
    {
        WorldHub.App.Services.Diagnostics.DebugConsole.Initialize();
        base.OnStartup(e);

        ShutdownMode =
            ShutdownMode.OnMainWindowClose;

        var settingsService =
            new AppSettingsService();

        var dataPath =
            settingsService.GetDataPath();

        var deviceIdentityService =
            new DeviceIdentityService(dataPath);

        var deviceId =
            deviceIdentityService.GetDeviceId();

        var appVersionService =
            new AppVersionService();

        var radminVpnDetector =
            new RadminVpnDetector();

        var localParticipantProvider =
            new LocalParticipantProvider(
                deviceIdentityService,
                settingsService,
                appVersionService,
                radminVpnDetector);

        var googleAuthService =
            new GoogleAuthService(dataPath);

        var googleDriveClient =
            new GoogleDriveClient(googleAuthService);

        var googleDriveStatusCache =
            new GoogleDriveStatusCache();

        googleDriveStatusCache.PreInitialize(
            googleAuthService.HasSavedToken());

        var networkProvider =
            new TcpNetworkProvider();

        var networkService =
            new NetworkService(networkProvider);

        // === Репозитории и сервисы, которые нужны WorldHubNetworkService ===

        var worldHubServersPath =
            Path.Combine(
                dataPath,
                "worldhub-servers");

        var worldHubServerRepository =
            new JsonWorldHubServerRepository(
                worldHubServersPath);

        var worldHubServerService =
            new WorldHubServerService(
                worldHubServerRepository);

        // === Теперь создаём WorldHubNetworkService ===

        _worldHubNetworkService =
            new WorldHubNetworkService(
                networkService,
                networkProvider,
                deviceId,
                () => settingsService.GetUserName(),
                () => appVersionService.GetVersion(),
                () => googleDriveStatusCache.GetStatusString(),
                async cancellationToken =>
                {
                    try
                    {
                        if (!googleAuthService.HasSavedToken())
                        {
                            return null;
                        }

                        return await googleDriveClient
                            .GetAccountEmailAsync(cancellationToken);
                    }
                    catch
                    {
                        return null;
                    }
                },
                async (remoteDeviceId, cancellationToken) =>
                {
                    try
                    {
                        var server = await worldHubServerService
                            .FindByParticipantDeviceIdAsync(
                                remoteDeviceId,
                                cancellationToken);

                        if (server is null)
                        {
                            return new ServerInfo(null, null);
                        }

                        return new ServerInfo(
                            server.GoogleDriveFolderId,
                            server.GoogleDriveOwnerEmail);
                    }
                    catch
                    {
                        return new ServerInfo(null, null);
                    }
                });

        _worldHubNetworkService.Start();

        // Фоновая проверка статуса Drive при старте.
        _ = Task.Run(async () =>
        {
            try
            {
                var result =
                    await googleDriveClient.CheckDriveAccessAsync();

                googleDriveStatusCache.Update(result);
            }
            catch
            {
                // Тихо: если проверка не удалась, статус останется Unknown.
            }
        });

        // === Остальные сервисы ===

        var serversPath =
            Path.Combine(
                dataPath,
                "servers");

        var serverRepository =
            new JsonServerRepository(
                serversPath);

        var serverService =
            new ServerService(
                serverRepository);

        var serverDetector = new ServerDetector();

        var processLauncher = new ServerProcessLauncher();

        var processManager =
            new DedicatedServerProcessManager(
                processLauncher);

        var mainWindow = new MainWindow(
            serverService,
            worldHubServerService,
            serverDetector,
            processManager,
            radminVpnDetector,
            _worldHubNetworkService,
            settingsService,
            googleDriveClient,
            googleDriveStatusCache,
            localParticipantProvider,
            deviceId,
            appVersionService);

        MainWindow = mainWindow;

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _worldHubNetworkService?.Dispose();
        _worldHubNetworkService = null;
        WorldHub.App.Services.Diagnostics.DebugConsole.Close();

        base.OnExit(e);
    }
}