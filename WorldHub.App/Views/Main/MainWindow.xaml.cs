using System.Windows;
using WorldHub.App.Services.Network;
using WorldHub.App.Services.Settings;
using WorldHub.App.Services.Update;
using WorldHub.App.Views.Connection;
using WorldHub.App.Views.Servers;
using WorldHub.App.Views.Settings;
using WorldHub.Core.Entities;
using WorldHub.Core.Interfaces;
using WorldHub.Infrastructure.Google;
using WorldHub.Infrastructure.Minecraft;
using WorldHub.Network.Services;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Main;

public partial class MainWindow
{
    private readonly ServersView _serversView;
    private readonly DedicatedServerProcessManager _processManager;
    private readonly SettingsView _settingsView;
    private readonly WorldHubServerService _worldHubServerService;
    private readonly ServerService _serverService;
    private readonly ConnectionView _connectionView;
    private readonly RadminVpnDetector _radminVpnDetector;

    public MainWindow(
        ServerService serverService,
        WorldHubServerService worldHubServerService,
        IServerDetector serverDetector,
        DedicatedServerProcessManager processManager,
        RadminVpnDetector radminVpnDetector,
        WorldHubNetworkService worldHubNetworkService,
        AppSettingsService appSettingsService,
        GoogleDriveClient googleDriveClient,
        GoogleDriveStatusCache googleDriveStatusCache,
        LocalParticipantProvider localParticipantProvider,
        string localDeviceId,
        AppVersionService appVersionService)
    {
        ArgumentNullException.ThrowIfNull(serverService);
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(serverDetector);
        ArgumentNullException.ThrowIfNull(processManager);
        ArgumentNullException.ThrowIfNull(radminVpnDetector);
        ArgumentNullException.ThrowIfNull(worldHubNetworkService);
        ArgumentNullException.ThrowIfNull(appSettingsService);
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(googleDriveStatusCache);
        ArgumentNullException.ThrowIfNull(localParticipantProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDeviceId);
        ArgumentNullException.ThrowIfNull(appVersionService);

        InitializeComponent();

        _processManager = processManager;
        _worldHubServerService = worldHubServerService;
        _serverService = serverService;
        _radminVpnDetector = radminVpnDetector;

        _connectionView = new ConnectionView(
            serverService,
            _worldHubServerService,
            worldHubNetworkService,
            _radminVpnDetector,
            googleDriveStatusCache,
            googleDriveClient,
            localParticipantProvider,
            localDeviceId);

        _serversView = new ServersView(
            serverService,
            serverDetector,
            processManager);

        _settingsView = new SettingsView();

        _settingsView.Initialize(
            appVersionService,
            appSettingsService,
            googleDriveClient,
            googleDriveStatusCache);

        SetActiveTab(NavTab.Servers);
        ContentHost.Content = _serversView;
    }

    private void ConnectionNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveTab(NavTab.Connection);
        ContentHost.Content = _connectionView;
    }

    private void SettingsNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveTab(NavTab.Settings);
        ContentHost.Content = _settingsView;
    }

    public void OpenServerSettings(Server server)
    {
        var settingsView =
            new MinecraftServerSettingsView(
                server,
                _serverService,
                _worldHubServerService,
                _processManager);

        settingsView.BackRequested += ServerSettingsView_BackRequested;

        SetActiveTab(NavTab.Servers);
        ContentHost.Content = settingsView;
    }

    private void ServerSettingsView_BackRequested(
        object? sender,
        EventArgs e)
    {
        SetActiveTab(NavTab.Servers);
        ContentHost.Content = _serversView;
    }

    private void ServersNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetActiveTab(NavTab.Servers);
        ContentHost.Content = _serversView;
    }

    private void SetActiveTab(NavTab tab)
    {
        var activeBackground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#1A1F29")!;
        var inactiveBackground = System.Windows.Media.Brushes.Transparent;

        var activeForeground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F2F4F7")!;
        var inactiveForeground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#8C95A3")!;

        ServersNavButton.Background = tab == NavTab.Servers ? activeBackground : inactiveBackground;
        ServersNavButton.Foreground = tab == NavTab.Servers ? activeForeground : inactiveForeground;
        ServersNavText.Foreground = tab == NavTab.Servers ? activeForeground : inactiveForeground;

        ConnectionNavButton.Background = tab == NavTab.Connection ? activeBackground : inactiveBackground;
        ConnectionNavButton.Foreground = tab == NavTab.Connection ? activeForeground : inactiveForeground;
        ConnectionNavText.Foreground = tab == NavTab.Connection ? activeForeground : inactiveForeground;

        SettingsNavButton.Background = tab == NavTab.Settings ? activeBackground : inactiveBackground;
        SettingsNavButton.Foreground = tab == NavTab.Settings ? activeForeground : inactiveForeground;
        SettingsNavText.Foreground = tab == NavTab.Settings ? activeForeground : inactiveForeground;
    }

    private enum NavTab
    {
        Servers,
        Connection,
        Settings
    }
}