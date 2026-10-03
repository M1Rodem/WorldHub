using System.IO;
using System.Windows;
using WorldHub.App.Services.Application;
using WorldHub.App.Services.Diagnostics;
using WorldHub.App.Services.Identity;
using WorldHub.App.Services.Network;
using WorldHub.App.Services.Settings;
using WorldHub.App.Services.Update;
using WorldHub.App.Views.Dialogs;
using WorldHub.Sync.Services;
using WorldHub.Sync.Transfer;

namespace WorldHub.App.Views.Main;

public partial class MainWindow
{
    private readonly WorldAppService _worldAppService;
    private readonly SnapshotAppService _snapshotAppService;
    private readonly WorldDeletionService _worldDeletionService;
    private readonly LocalPlayerIdentity _localPlayerIdentity;
    private readonly WorldHubNetworkService _worldHubNetworkService;
    private readonly WorldTransferOrchestrator _worldTransferOrchestrator;
    private readonly string _dataPath;

    public MainWindow(
        WorldAppService worldAppService,
        SnapshotAppService snapshotAppService,
        WorldDeletionService worldDeletionService,
        LocalPlayerIdentity localPlayerIdentity,
        WorldHubNetworkService worldHubNetworkService,
        WorldTransferOrchestrator worldTransferOrchestrator,
        string dataPath)
    {
        ArgumentNullException.ThrowIfNull(worldAppService);
        ArgumentNullException.ThrowIfNull(snapshotAppService);
        ArgumentNullException.ThrowIfNull(worldDeletionService);
        ArgumentNullException.ThrowIfNull(localPlayerIdentity);
        ArgumentNullException.ThrowIfNull(worldHubNetworkService);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPath);

        DebugConsole.Log("Creating MainWindow...");

        InitializeComponent();

        DebugConsole.Log("MainWindow UI initialized.");

        _worldAppService = worldAppService;
        _snapshotAppService = snapshotAppService;
        _worldDeletionService = worldDeletionService;
        _localPlayerIdentity = localPlayerIdentity;
        _worldHubNetworkService = worldHubNetworkService;
        _worldTransferOrchestrator = worldTransferOrchestrator;
        _dataPath = Path.GetFullPath(dataPath);

        ConnectionPage.Initialize(_worldHubNetworkService);

        WorldsPage.Initialize(
            _worldAppService,
            _worldTransferOrchestrator,
            _dataPath,
            ConnectionPage.TryGetFriendEndpoint,
            RefreshDataAsync,
            GoToConnectionPage);

        HistoryPage.Initialize(
            _worldAppService,
            _snapshotAppService,
            RefreshDataAsync);

        var appVersionService = new AppVersionService();
        var appSettingsService = new AppSettingsService();

        SettingsPage.Initialize(
            _worldDeletionService,
            WorldsPage.Worlds,
            RefreshDataAsync,
            appVersionService,
            appSettingsService);

        DebugConsole.Log(
            "Application services initialized.");

        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        DebugConsole.Log("MainWindow loaded.");

        await WorldsPage.RefreshAsync();
    }

    private void GoToConnectionPage()
    {
        ConnectionNavigationButton_Click(this, new RoutedEventArgs());
    }

    private void ConnectionNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log("Connection page requested.");

        WorldsPage.Visibility = Visibility.Collapsed;
        HistoryPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        ConnectionPage.Visibility = Visibility.Visible;

        PageTitleText.Text = "Подключение";
        PageDescriptionText.Text =
            "Подключение к другому WorldHub по IP-адресу и порту.";
    }

    public async Task<bool> ShowIncomingTransferConfirmationAsync(
        string worldName,
        int snapshotCount)
    {
        return await Dispatcher.InvokeAsync(
            () => DialogWindow.ShowTransferConfirmation(
                this,
                worldName,
                snapshotCount));
    }

    private async void HistoryNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log("History page requested.");

        WorldsPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        HistoryPage.Visibility = Visibility.Visible;
        ConnectionPage.Visibility = Visibility.Collapsed;

        PageTitleText.Text = "История";
        PageDescriptionText.Text = "История локальных версий миров";

        await HistoryPage.RefreshAsync();
    }

    private void MyWorldsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log("My worlds page requested.");

        ConnectionPage.Visibility = Visibility.Collapsed;
        HistoryPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Collapsed;
        WorldsPage.Visibility = Visibility.Visible;

        PageTitleText.Text = "Мои миры";
        PageDescriptionText.Text =
            "Управляйте мирами, версиями и подключениями";
    }

    private void SettingsNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log("Settings page requested.");

        WorldsPage.Visibility = Visibility.Collapsed;
        HistoryPage.Visibility = Visibility.Collapsed;
        SettingsPage.Visibility = Visibility.Visible;
        ConnectionPage.Visibility = Visibility.Collapsed;

        PageTitleText.Text = "Настройки";
        PageDescriptionText.Text =
            "Управление WorldHub, мирами и локальными данными";

        SettingsPage.RefreshWorlds(WorldsPage.Worlds);
    }

    public async Task RefreshDataAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(
                async () => await RefreshDataAsync());

            return;
        }

        await WorldsPage.RefreshAsync();

        if (HistoryPage.Visibility == Visibility.Visible)
        {
            await HistoryPage.RefreshAsync();
        }
    }
}