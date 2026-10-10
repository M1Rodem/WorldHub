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

using WorldHub.App.Services.Diagnostics;
using WorldHub.App.Views.Dialogs;
using WorldHub.Logging;

namespace WorldHub.App;

public partial class App : Application
{
    private WorldHubNetworkService? _worldHubNetworkService;

    protected override void OnStartup(
        StartupEventArgs e)
    {
        DebugConsole.Initialize();
        AppLog.SetLogger(new DebugConsoleLogger());
        AppLog.Log("WorldHub logging system initialized.");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                AppLog.Error($"[FATAL] AppDomain unhandled exception: {ex.Message}", ex);
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            AppLog.Error($"[ERROR] UI Dispatcher unhandled exception: {args.Exception.Message}", args.Exception);
        };

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

        var worldHubFolderSharingService =
            new WorldHubFolderSharingService(
                googleDriveClient,
                worldHubServerService);

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
                },
                async (remoteProfile, remoteIp, cancellationToken) =>
                {
                    try
                    {
                        var servers = await worldHubServerService.GetAllAsync(cancellationToken);
                        foreach (var server in servers)
                        {
                            var match = server.Participants.FirstOrDefault(p =>
                                (!string.IsNullOrWhiteSpace(p.DeviceId) &&
                                 string.Equals(p.DeviceId, remoteProfile.DeviceId, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrWhiteSpace(remoteIp) &&
                                 string.Equals(p.IpAddress, remoteIp, StringComparison.OrdinalIgnoreCase)));

                            if (match is not null)
                            {
                                match.DeviceId = remoteProfile.DeviceId;
                                match.UserName = remoteProfile.UserName;
                                match.PcName = remoteProfile.PcName;
                                match.WorldHubVersion = remoteProfile.WorldHubVersion;
                                match.WorldHubStatus = remoteProfile.WorldHubStatus;
                                match.GoogleDriveStatus = remoteProfile.GoogleDriveStatus;
                                match.GoogleEmail = remoteProfile.GoogleEmail;
                                match.IsPingAvailable = true;
                                match.IsWorldHubResponding = true;
                                match.LastCheckAtUtc = DateTimeOffset.UtcNow;
                                match.LastSeenAtUtc = DateTimeOffset.UtcNow;

                                await worldHubServerService.UpdateAsync(server, cancellationToken);

                                AppLog.Success(
                                    $"[PEER] Automatically updated remote participant '{match.UserName ?? match.IpAddress}' from incoming handshake.");

                                if (!string.IsNullOrWhiteSpace(server.GoogleDriveFolderId) &&
                                    !string.IsNullOrWhiteSpace(match.GoogleEmail))
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            await worldHubFolderSharingService.ShareWithAllParticipantsAsync(server);
                                        }
                                        catch (Exception ex)
                                        {
                                            AppLog.Warning($"Auto-sharing from incoming handshake failed: {ex.Message}", ex);
                                        }
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warning($"Failed to update participant from incoming handshake: {ex.Message}", ex);
                    }
                },
                async (inviteRequest, cancellationToken) =>
                {
                    try
                    {
                        var task = Current?.Dispatcher.InvokeAsync(async () =>
                        {
                            var owner = Current?.MainWindow;

                            var isGoogleConnected = googleAuthService.HasSavedToken() &&
                                string.Equals(googleDriveStatusCache.GetStatusString(), "DriveAvailable", StringComparison.OrdinalIgnoreCase);

                            if (!isGoogleConnected)
                            {
                                DialogWindow.ShowWarning(
                                    owner,
                                    "Google Drive не подключён",
                                    $"Вас приглашает {inviteRequest.OwnerUserName} в WorldHub-сервер «{inviteRequest.ServerName}».\n\n" +
                                    "Но у вас не подключён Google Drive!\n" +
                                    "Подключите Google-аккаунт в «Настройках», чтобы принимать приглашения и участвовать в сервере.");

                                AppLog.Warning($"[INVITE] Rejected invite for '{inviteRequest.ServerName}': Google Drive is not connected on this machine.");
                                return false;
                            }

                            var message =
                                $"Вас приглашает {inviteRequest.OwnerUserName} в WorldHub-сервер «{inviteRequest.ServerName}».\n\nПринять?";

                            var accepted = DialogWindow.ShowConfirmation(
                                owner,
                                "Приглашение в WorldHub-сервер",
                                message);

                            if (!accepted)
                            {
                                return false;
                            }

                            var localUserName = settingsService.GetUserName();
                            if (string.IsNullOrWhiteSpace(localUserName))
                            {
                                localUserName = Environment.UserName;
                            }

                            var radminAdapter = radminVpnDetector.FindAdapter();
                            var localIp = radminAdapter?.Address.ToString() ?? "127.0.0.1";

                            var createdServer = await worldHubServerService.CreateFromInviteAsync(
                                inviteRequest.ServerName,
                                inviteRequest.FolderId,
                                inviteRequest.OwnerEmail,
                                inviteRequest.OwnerDeviceId,
                                inviteRequest.OwnerUserName,
                                inviteRequest.Participants,
                                deviceId,
                                localUserName,
                                Environment.MachineName,
                                localIp,
                                cancellationToken);

                            if (!string.IsNullOrWhiteSpace(createdServer.GoogleDriveFolderId))
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await googleDriveClient.CheckFolderAccessAsync(createdServer.GoogleDriveFolderId);
                                    }
                                    catch
                                    {
                                    }
                                });
                            }

                            if (Current?.Dispatcher is { } dispatcher)
                            {
                                dispatcher.Invoke(() =>
                                {
                                    if (Current?.MainWindow is MainWindow mainWindow)
                                    {
                                        mainWindow.ReloadServerList(createdServer.Id);
                                    }
                                });
                            }

                            return true;
                        });

                        if (task is null)
                        {
                            return false;
                        }

                        return await task.Task.Unwrap();
                    }
                    catch (Exception exception)
                    {
                        AppLog.Error($"[INVITE] Error handling incoming invite: {exception.Message}", exception);
                        return false;
                    }
                },
                async (serverDeletedNotification, cancellationToken) =>
                {
                    try
                    {
                        var servers = await worldHubServerService.GetAllAsync(cancellationToken);
                        var matchingServer = servers.FirstOrDefault(s =>
                            string.Equals(s.Name, serverDeletedNotification.ServerName, StringComparison.OrdinalIgnoreCase) &&
                            (string.IsNullOrWhiteSpace(serverDeletedNotification.HostDeviceId) ||
                             string.Equals(s.HostDeviceId, serverDeletedNotification.HostDeviceId, StringComparison.OrdinalIgnoreCase)));

                        if (matchingServer is not null)
                        {
                            AppLog.Log($"[SYNC] Host deleted WorldHub server '{matchingServer.Name}'. Cleaning up locally...");
                            await worldHubServerService.DeleteWithCleanupAsync(
                                matchingServer.Id,
                                serverService,
                                googleDriveClient: null,
                                deleteCloudFolder: false,
                                cancellationToken: cancellationToken);

                            Current?.Dispatcher.Invoke(() =>
                            {
                                if (Current?.MainWindow is MainWindow mw)
                                {
                                    mw.ReloadServerList();
                                }

                                var owner = Current?.MainWindow;
                                DialogWindow.ShowInformation(
                                    owner,
                                    "WorldHub-сервер удалён",
                                    $"Хост удалил WorldHub-сервер «{matchingServer.Name}».\nСервер был удалён из вашего списка.");
                            });
                        }
                    }
                    catch (Exception exception)
                    {
                        AppLog.Error($"[SYNC] Error handling remote server deletion: {exception.Message}", exception);
                    }
                },
                async (participantLeftNotification, cancellationToken) =>
                {
                    try
                    {
                        var servers = await worldHubServerService.GetAllAsync(cancellationToken);
                        var matchingServer = servers.FirstOrDefault(s =>
                            string.Equals(s.Name, participantLeftNotification.ServerName, StringComparison.OrdinalIgnoreCase));

                        if (matchingServer is not null)
                        {
                            var target = matchingServer.Participants.FirstOrDefault(p =>
                                !string.IsNullOrWhiteSpace(p.DeviceId) &&
                                string.Equals(p.DeviceId, participantLeftNotification.ParticipantDeviceId, StringComparison.OrdinalIgnoreCase));

                            if (target is not null)
                            {
                                matchingServer.Participants.Remove(target);
                                await worldHubServerService.UpdateAsync(matchingServer, cancellationToken);
                                AppLog.Success($"[SYNC] Removed participant '{participantLeftNotification.ParticipantUserName}' from '{matchingServer.Name}' because they left.");

                                Current?.Dispatcher.Invoke(() =>
                                {
                                    if (Current?.MainWindow is MainWindow mw)
                                    {
                                        mw.ReloadCurrentParticipantsIfMatches(matchingServer.Id);
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        AppLog.Error($"[SYNC] Error handling remote participant left: {exception.Message}", exception);
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
            worldHubFolderSharingService,
            deviceId,
            appVersionService);

        MainWindow = mainWindow;

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _worldHubNetworkService?.Dispose();
        }
        catch
        {
        }
        _worldHubNetworkService = null;

        try
        {
            AppLog.Log("WorldHub shutting down.");
            AppLog.SetLogger(NullLogger.Instance);
        }
        catch
        {
        }

        try
        {
            DebugConsole.Close();
        }
        catch
        {
        }

        base.OnExit(e);

        Environment.Exit(e.ApplicationExitCode);
    }
}