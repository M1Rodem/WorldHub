using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WorldHub.App.Services.Network;
using WorldHub.App.Views.Dialogs;
using WorldHub.App.Views.Settings;
using WorldHub.Core.Entities;
using WorldHub.Infrastructure.Google;
using WorldHub.Network.Services;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Connection;

public partial class ConnectionView : UserControl
{
    private readonly WorldHubServerService _worldHubServerService;
    private readonly ServerService _serverService;
    private readonly WorldHubParticipantCheckService _checkService;
    private readonly RadminVpnDetector _radminVpnDetector;
    private readonly GoogleDriveStatusCache _googleDriveStatusCache;
    private readonly LocalParticipantProvider _localParticipantProvider;
    private readonly DispatcherTimer _refreshTimer;

    private CancellationTokenSource? _pageCts;
    private bool _isLoading;
    private Guid? _selectedServerId;
    private WorldHubServer? _currentServer;

    public ConnectionView(
        ServerService serverService,
        WorldHubServerService worldHubServerService,
        WorldHubNetworkService networkService,
        RadminVpnDetector radminVpnDetector,
        GoogleDriveStatusCache googleDriveStatusCache,
        GoogleDriveClient googleDriveClient,
        LocalParticipantProvider localParticipantProvider,
        string localDeviceId)
    {
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(serverService);
        ArgumentNullException.ThrowIfNull(radminVpnDetector);
        ArgumentNullException.ThrowIfNull(googleDriveStatusCache);
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(localParticipantProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDeviceId);

        _worldHubServerService = worldHubServerService;
        _serverService = serverService;
        _radminVpnDetector = radminVpnDetector;
        _googleDriveStatusCache = googleDriveStatusCache;
        _localParticipantProvider = localParticipantProvider;

        _checkService = new WorldHubParticipantCheckService(
            networkService,
            worldHubServerService,
            localDeviceId);

        InitializeComponent();

        RadminCard.Initialize(radminVpnDetector);
        FolderSection.Initialize(googleDriveClient, worldHubServerService);
        FolderSection.ServerUpdated += (_, updatedServer) => _currentServer = updatedServer;

        ParticipantsSection.Initialize(_checkService, worldHubServerService, localDeviceId);
        ParticipantsSection.ParticipantCountChanged += (_, count) => UpdateParticipantCountUi(count);
        ParticipantsSection.InviteRequested += async vm =>
        {
            var owner = Window.GetWindow(this);
            if (owner is not null)
            {
                await FolderSection.InviteParticipantAsync(vm, owner);
            }
        };

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };

        _refreshTimer.Tick += RefreshTimer_Tick;
        _googleDriveStatusCache.Updated += GoogleDriveStatusCache_Updated;

        Loaded += ConnectionView_Loaded;
        Unloaded += ConnectionView_Unloaded;
    }

    private void UpdateParticipantCountUi(int count)
    {
        ParticipantCountText.Text =
            count == 0
                ? "Нет участников"
                : $"Участников: {count}";
    }

    private async void AddParticipantButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner is not null)
        {
            await ParticipantsSection.AddParticipantAsync(owner);
        }
    }

    private async void ConnectionView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _pageCts?.Cancel();
        _pageCts?.Dispose();
        var cts = new CancellationTokenSource();
        _pageCts = cts;

        await RadminCard.RefreshStatusAsync();

        if (cts.IsCancellationRequested)
        {
            return;
        }

        await LoadWorldHubServersAsync();

        if (cts.IsCancellationRequested)
        {
            return;
        }

        if (_googleDriveStatusCache.IsInitialized &&
            _selectedServerId is not null)
        {
            await ParticipantsSection.CheckAllParticipantsAsync(
                _selectedServerId.Value,
                cts.Token);
        }

        if (cts.IsCancellationRequested)
        {
            return;
        }

        _refreshTimer.Start();
    }

    private void ConnectionView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _refreshTimer.Stop();

        _pageCts?.Cancel();
        _pageCts?.Dispose();
        _pageCts = null;
    }

    private void GoogleDriveStatusCache_Updated(
        object? sender,
        EventArgs e)
    {
        Dispatcher.BeginInvoke(
            new Action(async () =>
            {
                try
                {
                    if (_selectedServerId is null ||
                        _pageCts is null ||
                        _pageCts.IsCancellationRequested)
                    {
                        return;
                    }

                    await ParticipantsSection.CheckAllParticipantsAsync(
                        _selectedServerId.Value,
                        _pageCts.Token);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Drive update handler failed: {exception.Message}");
                }
            }));
    }

    private async void RefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (_selectedServerId is null ||
            _pageCts is null)
        {
            return;
        }

        await ParticipantsSection.CheckAllParticipantsAsync(
            _selectedServerId.Value,
            _pageCts.Token);
    }

    private async Task LoadWorldHubServersAsync(Guid? selectedId = null)
    {
        try
        {
            _isLoading = true;

            var servers = await _worldHubServerService.GetAllAsync();

            WorldHubServerComboBox.ItemsSource = servers;

            if (selectedId.HasValue)
            {
                WorldHubServerComboBox.SelectedValue = selectedId.Value;
            }
            else if (WorldHubServerComboBox.SelectedValue is null &&
                     servers.Count > 0)
            {
                WorldHubServerComboBox.SelectedIndex = 0;
            }

            await UpdateSelectedServerAsync();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                $"Не удалось загрузить WorldHub-серверы.\n\n{exception.Message}",
                "WorldHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void WorldHubServerComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        await UpdateSelectedServerAsync();
    }

    private async Task UpdateSelectedServerAsync()
    {
        if (WorldHubServerComboBox.SelectedItem is not WorldHubServer server)
        {
            _selectedServerId = null;
            _currentServer = null;
            ParticipantsSection.Clear();
            FolderSection.SetServer(null);

            DeleteWorldHubServerButton.IsEnabled = false;

            EmptyStatePanel.Visibility = Visibility.Visible;
            SelectedServerScrollViewer.Visibility = Visibility.Collapsed;
            SelectedServerPanel.Visibility = Visibility.Collapsed;
            return;
        }

        _selectedServerId = server.Id;
        _currentServer = server;

        DeleteWorldHubServerButton.IsEnabled = true;

        SelectedServerNameText.Text = server.Name;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        SelectedServerScrollViewer.Visibility = Visibility.Visible;
        SelectedServerPanel.Visibility = Visibility.Visible;

        FolderSection.SetServer(server);
        ParticipantsSection.SetServer(server.Id, server.Name);
        await ParticipantsSection.ReloadParticipantsAsync(server.Id);
    }

    private async void CreateWorldHubServerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);

        if (owner is null)
        {
            return;
        }

        RadminVpnAdapterInfo? adapter;

        try
        {
            adapter = await Task.Run(() =>
                _radminVpnDetector.FindAdapter());
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                owner,
                $"Ошибка проверки Radmin VPN.\n\n{exception.Message}",
                "WorldHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        if (adapter is null)
        {
            MessageBox.Show(
                owner,
                "Подключите Radmin VPN и повторите попытку.",
                "WorldHub",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var window = new CreateWorldHubServerWindow(
            _worldHubServerService,
            _localParticipantProvider)
        {
            Owner = owner
        };

        if (window.ShowDialog() != true ||
            window.CreatedServer is null)
        {
            return;
        }

        await LoadWorldHubServersAsync(window.CreatedServer.Id);
    }

    private async void DeleteWorldHubServerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedServerId is null ||
            _currentServer is null)
        {
            return;
        }

        var owner = Window.GetWindow(this);

        if (owner is null)
        {
            return;
        }

        var serverName = _currentServer.Name;

        var confirmed = DialogWindow.ShowConfirmation(
            owner,
            "Удаление WorldHub-сервера",
            $"Удалить WorldHub-сервер «{serverName}»?\n\n" +
            "• Удалятся связи с Minecraft-серверами.\n" +
            "• Отвяжется общая папка Google Drive. " +
            "Сама папка в Drive останется — удалите её вручную, если нужно.\n" +
            "• У друзей сервер останется в их локальных данных, " +
            "пока они не удалят его вручную.\n\n" +
            "Это действие нельзя отменить.");

        if (!confirmed)
        {
            return;
        }

        DeleteWorldHubServerButton.IsEnabled = false;

        try
        {
            var token = _pageCts?.Token ?? CancellationToken.None;

            await _worldHubServerService.DeleteWithCleanupAsync(
                _selectedServerId.Value,
                _serverService,
                token);

            _checkService.RemoveGate(_selectedServerId.Value);

            _selectedServerId = null;
            _currentServer = null;
            ParticipantsSection.Clear();
            FolderSection.SetServer(null);

            await LoadWorldHubServersAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                owner,
                $"Не удалось удалить WorldHub-сервер.\n\n{exception.Message}",
                "WorldHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            DeleteWorldHubServerButton.IsEnabled =
                _selectedServerId is not null;
        }
    }
}