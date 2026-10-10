using System.Windows;
using WorldHub.App.Services.Diagnostics;
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
    private readonly GoogleDriveStatusCache _googleDriveStatusCache;
    private readonly GoogleDriveClient _googleDriveClient;
    private readonly AppSettingsService _appSettingsService;
    private readonly string _localDeviceId;

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
        WorldHubFolderSharingService worldHubFolderSharingService,
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

        _localDeviceId = localDeviceId;
        _appSettingsService = appSettingsService;
        _googleDriveClient = googleDriveClient;
        _processManager = processManager;
        _worldHubServerService = worldHubServerService;
        _serverService = serverService;
        _radminVpnDetector = radminVpnDetector;
        _googleDriveStatusCache = googleDriveStatusCache;

        _connectionView = new ConnectionView(
            serverService,
            _worldHubServerService,
            worldHubNetworkService,
            _radminVpnDetector,
            googleDriveStatusCache,
            googleDriveClient,
            localParticipantProvider,
            worldHubFolderSharingService,
            localDeviceId,
            appSettingsService);

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
                _processManager,
                _googleDriveStatusCache,
                _localDeviceId,
                _googleDriveClient,
                _appSettingsService);

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

    public void ReloadCurrentParticipantsIfMatches(Guid serverId)
    {
        _ = _connectionView.ReloadParticipantsIfMatchesAsync(serverId);
    }

    public void ReloadServerList(Guid? selectServerId = null)
    {
        _ = _connectionView.ReloadServerListAsync(selectServerId);
    }

    protected override void OnClosed(EventArgs e)
    {
        DebugConsole.Close();
        base.OnClosed(e);
        Application.Current?.Shutdown();
    }

    private void SetActiveTab(NavTab tab)
    {
        var activeBackground = (System.Windows.Media.Brush?)Application.Current.TryFindResource("AccentPurpleSubtleBrush")
            ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#1F1934")!;
        var activeBorder = (System.Windows.Media.Brush?)Application.Current.TryFindResource("AccentPurpleBorderBrush")
            ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#53348A")!;
        var activeForeground = (System.Windows.Media.Brush?)Application.Current.TryFindResource("TextPrimaryBrush")
            ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F3F5F9")!;

        var inactiveBackground = System.Windows.Media.Brushes.Transparent;
        var inactiveBorder = System.Windows.Media.Brushes.Transparent;
        var inactiveForeground = (System.Windows.Media.Brush?)Application.Current.TryFindResource("TextSecondaryBrush")
            ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#8E9AB0")!;

        void ApplyNav(System.Windows.Controls.Button btn, System.Windows.Controls.TextBlock txt, bool isActive)
        {
            btn.Background = isActive ? activeBackground : inactiveBackground;
            btn.BorderBrush = isActive ? activeBorder : inactiveBorder;
            btn.Foreground = isActive ? activeForeground : inactiveForeground;
            txt.Foreground = isActive ? activeForeground : inactiveForeground;
            txt.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Medium;
        }

        ApplyNav(ServersNavButton, ServersNavText, tab == NavTab.Servers);
        ApplyNav(ConnectionNavButton, ConnectionNavText, tab == NavTab.Connection);
        ApplyNav(SettingsNavButton, SettingsNavText, tab == NavTab.Settings);
    }

    private enum NavTab
    {
        Servers,
        Connection,
        Settings
    }
}