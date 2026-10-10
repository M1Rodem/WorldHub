using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WorldHub.App.Services.Network;
using WorldHub.App.Services.Settings;
using WorldHub.App.Views.Dialogs;
using WorldHub.App.Views.Settings;
using WorldHub.Core.Entities;
using WorldHub.Infrastructure.Google;
using WorldHub.Logging;
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
    private readonly WorldHubNetworkService _networkService;
    private readonly GoogleDriveClient _googleDriveClient;
    private readonly string _localDeviceId;
    private readonly DispatcherTimer _refreshTimer;

    private CancellationTokenSource? _pageCts;
    private bool _isLoading;
    private bool _isAutoChecking;
    private Guid? _selectedServerId;
    private WorldHubServer? _currentServer;

    private readonly AppSettingsService _appSettingsService;

    public ConnectionView(
       ServerService serverService,
       WorldHubServerService worldHubServerService,
       WorldHubNetworkService networkService,
       RadminVpnDetector radminVpnDetector,
       GoogleDriveStatusCache googleDriveStatusCache,
       GoogleDriveClient googleDriveClient,
       LocalParticipantProvider localParticipantProvider,
       WorldHubFolderSharingService worldHubFolderSharingService,
       string localDeviceId,
       AppSettingsService appSettingsService)
    {
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(networkService);
        ArgumentNullException.ThrowIfNull(serverService);
        ArgumentNullException.ThrowIfNull(radminVpnDetector);
        ArgumentNullException.ThrowIfNull(googleDriveStatusCache);
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(localParticipantProvider);
        ArgumentNullException.ThrowIfNull(worldHubFolderSharingService);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDeviceId);
        ArgumentNullException.ThrowIfNull(appSettingsService);

        _worldHubServerService = worldHubServerService;
        _networkService = networkService;
        _serverService = serverService;
        _radminVpnDetector = radminVpnDetector;
        _googleDriveStatusCache = googleDriveStatusCache;
        _googleDriveClient = googleDriveClient;
        _localParticipantProvider = localParticipantProvider;
        _localDeviceId = localDeviceId;
        _appSettingsService = appSettingsService;

        _checkService = new WorldHubParticipantCheckService(
            networkService,
            worldHubServerService,
            localDeviceId,
            () => appSettingsService.GetUserName(),
            null,
            () => googleDriveStatusCache.GetStatusString(),
            async cancellationToken =>
            {
                try
                {
                    return await googleDriveClient.GetAccountEmailAsync(cancellationToken);
                }
                catch
                {
                    return null;
                }
            });

        InitializeComponent();

        RadminCard.Initialize(radminVpnDetector);
        FolderSection.Initialize(
            googleDriveClient,
            worldHubServerService,
            worldHubFolderSharingService);

        FolderSection.ServerUpdated += (_, updatedServer) => _currentServer = updatedServer;

        ParticipantsSection.Initialize(
            _checkService,
            worldHubServerService,
            localDeviceId,
            () => appSettingsService.GetUserName(),
            networkService);

        ParticipantsSection.ParticipantCountChanged += (_, count) => UpdateParticipantCountUi(count);

        ParticipantsSection.ParticipantCheckCompleted += async (_, _) =>
        {
            if (_selectedServerId.HasValue)
            {
                var freshServer = await _worldHubServerService.GetByIdAsync(_selectedServerId.Value);
                if (freshServer is not null)
                {
                    _currentServer = freshServer;
                    FolderSection.SetServer(_currentServer);
                }
            }

            await FolderSection.RefreshFolderAccessAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    await FolderSection.ShareWithCurrentParticipantsAsync();
                }
                catch (Exception ex)
                {
                    AppLog.Warning($"Auto-share background error: {ex.Message}", ex);
                }
            });
        };

        _appSettingsService.UserNameChanged += async (_, newUserName) =>
        {
            try
            {
                var servers = await _worldHubServerService.GetAllAsync();
                foreach (var server in servers)
                {
                    var self = server.Participants.FirstOrDefault(p =>
                        string.Equals(p.DeviceId, localDeviceId, StringComparison.OrdinalIgnoreCase));

                    if (self is not null && self.UserName != newUserName)
                    {
                        self.UserName = newUserName;
                        await _worldHubServerService.UpdateAsync(server);
                    }
                }

                if (_selectedServerId is not null)
                {
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await ParticipantsSection.ReloadParticipantsAsync(_selectedServerId.Value);
                    });
                }
            }
            catch
            {
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

    private void UpdateParticipantCountUi(
        int count)
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
        if (_isAutoChecking)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            new Action(async () =>
            {
                if (_isAutoChecking)
                {
                    return;
                }

                try
                {
                    if (_selectedServerId is null ||
                        _pageCts is null ||
                        _pageCts.IsCancellationRequested)
                    {
                        return;
                    }

                    _isAutoChecking = true;
                    try
                    {
                        AppLog.Separator(
                            $"Drive status updated, re-checking '{_currentServer?.Name ?? "server"}'");

                        await ParticipantsSection.CheckAllParticipantsAsync(
                            _selectedServerId.Value,
                            _pageCts.Token);
                    }
                    finally
                    {
                        _isAutoChecking = false;
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    AppLog.Warning(
                        $"Drive update handler failed: {exception.Message}",
                        exception);
                }
            }));
    }

    private async void RefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (_selectedServerId is null ||
            _pageCts is null ||
            _isAutoChecking)
        {
            return;
        }

        _isAutoChecking = true;
        try
        {
            AppLog.Separator(
                $"Auto-check of '{_currentServer?.Name ?? "server"}'");

            await ParticipantsSection.CheckAllParticipantsAsync(
                _selectedServerId.Value,
                _pageCts.Token);
        }
        finally
        {
            _isAutoChecking = false;
        }
    }

    public async Task ReloadServerListAsync(Guid? selectServerId = null)
    {
        await LoadWorldHubServersAsync(selectServerId);
    }

    public async Task ReloadParticipantsIfMatchesAsync(Guid serverId)
    {
        if (_selectedServerId == serverId)
        {
            await ParticipantsSection.ReloadParticipantsAsync(serverId);
        }
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

        // Определяем, является ли локальный пользователь хостом сервера
        var isHost = false;
        var firstParticipant = server.Participants.FirstOrDefault();
        if (firstParticipant is not null && !string.IsNullOrWhiteSpace(firstParticipant.DeviceId))
        {
            isHost = string.Equals(firstParticipant.DeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrWhiteSpace(server.HostDeviceId))
        {
            isHost = string.Equals(server.HostDeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            isHost = true;
        }

        DeleteWorldHubServerButton.Content = isHost ? "Удалить" : "Покинуть";
        DeleteWorldHubServerButton.IsEnabled = true;

        AddParticipantButton.Visibility = isHost ? Visibility.Visible : Visibility.Collapsed;

        SelectedServerNameText.Text = server.Name;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        SelectedServerScrollViewer.Visibility = Visibility.Visible;
        SelectedServerPanel.Visibility = Visibility.Visible;

        FolderSection.SetHostStatus(isHost);
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

        // Хост определяется по первичному участнику (server.Participants.FirstOrDefault())
        // либо по server.HostDeviceId для совместимости.
        var isHost = false;
        var firstParticipant = _currentServer.Participants.FirstOrDefault();
        if (firstParticipant is not null && !string.IsNullOrWhiteSpace(firstParticipant.DeviceId))
        {
            isHost = string.Equals(firstParticipant.DeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrWhiteSpace(_currentServer.HostDeviceId))
        {
            isHost = string.Equals(_currentServer.HostDeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // Если участников нет, считаем локальным
            isHost = true;
        }

        if (isHost)
        {
            // === ВЕТКА 1: СОЗДАТЕЛЬ (ХОСТ) УДАЛЯЕТ СЕРВЕР ===
            var confirmed = DialogWindow.ShowConfirmation(
                owner,
                "Удаление WorldHub-сервера",
                $"Вы являетесь хостом сервера «{serverName}».\n\n" +
                "Удалить этот WorldHub-сервер?\n" +
                "• WorldHub-сервер будет удалён.\n" +
                "• Общая папка Google Drive будет удалена вместе с сервером.\n" +
                "• Отвязка локальных Minecraft-серверов.\n" +
                "• У друзей сервер будет удалён автоматически.\n\n" +
                "Это действие нельзя отменить.");

            if (!confirmed)
            {
                return;
            }

            DeleteWorldHubServerButton.IsEnabled = false;

            try
            {
                var token = _pageCts?.Token ?? CancellationToken.None;
                var serverId = _selectedServerId.Value;

                // Оповещаем всех остальных участников по сети об удалении сервера хостом
                var otherParticipants = _currentServer.Participants
                    .Where(p => !string.Equals(p.DeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(p.IpAddress))
                    .ToList();

                if (otherParticipants.Count > 0 && _networkService is not null)
                {
                    var notification = new WorldHub.Network.Protocol.ServerDeletedNotification(
                        _currentServer.Name,
                        _localDeviceId);

                    _ = Task.Run(async () =>
                    {
                        var tasks = otherParticipants.Select(async participant =>
                        {
                            try
                            {
                                await _networkService.NotifyServerDeletedAsync(
                                    participant.IpAddress,
                                    notification,
                                    CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                AppLog.Warning($"Failed to notify {participant.IpAddress} about server deletion: {ex.Message}");
                            }
                        });

                        await Task.WhenAll(tasks);
                    });
                }

                var deleteResult = await _worldHubServerService.DeleteWithCleanupAsync(
                    serverId,
                    _serverService,
                    _googleDriveClient,
                    deleteCloudFolder: true,
                    cancellationToken: token);

                if (deleteResult is not null && !deleteResult.Success)
                {
                    DialogWindow.ShowWarning(
                        owner,
                        "Папка Google Drive не удалена",
                        $"WorldHub-сервер «{serverName}» удалён локально, но общую папку в Google Drive удалить не удалось:\n{deleteResult.Message}");
                }

                _checkService.RemoveGate(serverId);

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
        else
        {
            // === ВЕТКА 2: НЕ СОЗДАТЕЛЬ ПОКИДАЕТ СЕРВЕР (УДАЛЯЕТ СВОЙ АККАУНТ ИЗ СЕРВЕРА) ===
            var hostDisplay = firstParticipant?.UserName ?? _currentServer.HostUserName ?? "Хост";

            var confirmed = DialogWindow.ShowConfirmation(
                owner,
                "Выход из WorldHub-сервера",
                $"Вы не являетесь хостом сервера «{serverName}» (хост: {hostDisplay}).\n\n" +
                "Покинуть этот WorldHub-сервер?\n" +
                "• Сервер будет удалён из вашего приложения.\n" +
                "• Общая папка Google Drive не будет удалена (вы не её владелец).\n" +
                "• Вы будете удалены из списка участников у всех друзей.\n" +
                "• Связи с вашими локальными Minecraft-серверами будут сброшены.\n\n" +
                "Продолжить?");

            if (!confirmed)
            {
                return;
            }

            DeleteWorldHubServerButton.IsEnabled = false;

            try
            {
                var token = _pageCts?.Token ?? CancellationToken.None;
                var serverId = _selectedServerId.Value;

                // Оповещаем всех остальных участников (включая хоста), что мы покинули сервер
                var otherParticipants = _currentServer.Participants
                    .Where(p => !string.Equals(p.DeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase) &&
                                !string.IsNullOrWhiteSpace(p.IpAddress))
                    .ToList();

                var currentUserName = _appSettingsService.GetUserName();
                if (string.IsNullOrWhiteSpace(currentUserName))
                {
                    currentUserName = Environment.UserName;
                }

                if (otherParticipants.Count > 0 && _networkService is not null)
                {
                    var notification = new WorldHub.Network.Protocol.ParticipantLeftNotification(
                        _currentServer.Name,
                        _localDeviceId,
                        currentUserName);

                    _ = Task.Run(async () =>
                    {
                        foreach (var participant in otherParticipants)
                        {
                            try
                            {
                                await _networkService.NotifyParticipantLeftAsync(
                                    participant.IpAddress,
                                    notification);
                            }
                            catch (Exception ex)
                            {
                                AppLog.Warning($"Failed to notify {participant.IpAddress} that we left: {ex.Message}");
                            }
                        }
                    });
                }

                // Удаляем локально у себя этот сервер и отвязываем локальные Minecraft-серверы
                await _worldHubServerService.DeleteWithCleanupAsync(
                    serverId,
                    _serverService,
                    _googleDriveClient,
                    deleteCloudFolder: false,
                    cancellationToken: token);

                _checkService.RemoveGate(serverId);

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
                    $"Не удалось покинуть WorldHub-сервер.\n\n{exception.Message}",
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
}
