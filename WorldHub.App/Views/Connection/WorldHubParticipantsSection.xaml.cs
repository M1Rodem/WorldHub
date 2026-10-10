using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services.Network;
using WorldHub.App.Views.Dialogs;
using WorldHub.Core.Entities;
using WorldHub.Logging;
using WorldHub.Network.Protocol;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Connection;

public partial class WorldHubParticipantsSection : UserControl
{
    private WorldHubParticipantCheckService? _checkService;
    private WorldHubServerService? _worldHubServerService;
    private WorldHubNetworkService? _networkService;
    private string _localDeviceId = string.Empty;
    private Func<string>? _userNameProvider;
    private Guid? _selectedServerId;
    private string? _selectedServerName;
    private bool _isChecking;
    public ObservableCollection<WorldHubParticipantViewModel> Participants { get; } = new();
    public event EventHandler<int>? ParticipantCountChanged;
    public event EventHandler? ParticipantCheckCompleted;
    public WorldHubParticipantsSection()
    {
        InitializeComponent();
        ParticipantsItemsControl.ItemsSource = Participants;
    }

    public void Initialize(
        WorldHubParticipantCheckService checkService,
        WorldHubServerService worldHubServerService,
        string localDeviceId,
        Func<string>? userNameProvider = null,
        WorldHubNetworkService? networkService = null)
    {
        ArgumentNullException.ThrowIfNull(checkService);
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDeviceId);

        _checkService = checkService;
        _worldHubServerService = worldHubServerService;
        _localDeviceId = localDeviceId;
        _userNameProvider = userNameProvider;
        _networkService = networkService;
    }

    public void SetServer(Guid? serverId, string? serverName = null)
    {
        _selectedServerId = serverId;
        _selectedServerName = serverName;
    }

    public void Clear()
    {
        _selectedServerId = null;
        _selectedServerName = null;
        Participants.Clear();
        UpdateParticipantsUi();
    }

    public async Task ReloadParticipantsAsync(Guid serverId)
    {
        if (_worldHubServerService is null)
        {
            return;
        }

        var server = await _worldHubServerService.GetByIdAsync(serverId);

        if (server is null || _selectedServerId != serverId)
        {
            if (_selectedServerId == serverId)
            {
                Participants.Clear();
                UpdateParticipantsUi();
            }
            return;
        }

        // Обновляем никнейм себя (локального участника), если он был изменён в настройках
        var currentLocalUserName = _userNameProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(currentLocalUserName) && !string.IsNullOrWhiteSpace(_localDeviceId))
        {
            var selfParticipant = server.Participants.FirstOrDefault(p =>
                string.Equals(p.DeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase));

            if (selfParticipant is not null && selfParticipant.UserName != currentLocalUserName)
            {
                selfParticipant.UserName = currentLocalUserName;
                await _worldHubServerService.UpdateAsync(server);
            }
        }

        Participants.Clear();

        foreach (var participant in server.Participants)
        {
            Participants.Add(new WorldHubParticipantViewModel(
                participant,
                _localDeviceId));
        }

        UpdateParticipantsUi();
    }

    private void UpdateParticipantsUi()
    {
        ParticipantsEmptyText.Visibility =
            Participants.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        ParticipantCountChanged?.Invoke(this, Participants.Count);
    }

    public async Task CheckAllParticipantsAsync(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        if (_isChecking || _checkService is null)
        {
            return;
        }

        _isChecking = true;

        try
        {
            await _checkService.CheckAllAsync(
                serverId,
                cancellationToken);

            if (_selectedServerId == serverId && !cancellationToken.IsCancellationRequested)
            {
                await ReloadParticipantsAsync(serverId);
                ParticipantCheckCompleted?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            AppLog.Warning($"CheckAll failed: {exception.Message}", exception);
        }
        finally
        {
            _isChecking = false;
        }
    }

    public async Task AddParticipantAsync(Window owner)
    {
        if (_selectedServerId is null || _checkService is null || _worldHubServerService is null)
        {
            return;
        }

        var window = new AddParticipantWindow
        {
            Owner = owner
        };

        if (window.ShowDialog() != true ||
            string.IsNullOrWhiteSpace(window.IpAddress))
        {
            return;
        }

        var serverId = _selectedServerId.Value;
        var server = await _worldHubServerService.GetByIdAsync(serverId);
        if (server is null)
        {
            return;
        }

        var targetIp = window.IpAddress.Trim();

        // 1. Проверяем, нет ли уже участника с таким IP
        if (server.Participants.Any(p => string.Equals(p.IpAddress, targetIp, StringComparison.OrdinalIgnoreCase)))
        {
            DialogWindow.ShowWarning(
                owner,
                "Уже существует",
                $"Участник с IP {targetIp} уже добавлен в этот сервер.");
            return;
        }

        if (_networkService is null)
        {
            DialogWindow.ShowError(
                owner,
                "Ошибка сети",
                "Сетевая служба недоступна.");
            return;
        }

        var inviteRequest = new InviteRequest(
            serverName: server.Name,
            folderId: server.GoogleDriveFolderId,
            ownerEmail: server.GoogleDriveOwnerEmail,
            ownerUserName: _userNameProvider?.Invoke() ?? Environment.UserName,
            ownerDeviceId: _localDeviceId,
            participants: server.Participants
                .Select(p => new InviteParticipant(
                    p.DeviceId ?? "",
                    p.IpAddress,
                    p.UserName ?? "",
                    p.PcName ?? ""))
                .ToList());

        AppLog.Separator($"Sending invite to {targetIp} for '{server.Name}'");

        DialogWindow.ShowInformation(
            owner,
            "Приглашение отправлено",
            $"Приглашение отправлено на {targetIp}.\nОжидаем ответ друга...");

        var serverName = server.Name;

        _ = Task.Run(async () =>
        {
            try
            {
                var accepted = await _networkService.SendInviteAsync(
                    targetIp,
                    inviteRequest,
                    CancellationToken.None);

                await Dispatcher.InvokeAsync(async () =>
                {
                    if (accepted)
                    {
                        try
                        {
                            var participant = await _checkService.AddParticipantAsync(
                                serverId,
                                targetIp,
                                CancellationToken.None);

                            await _checkService.CheckAsync(
                                serverId,
                                participant.Id,
                                CancellationToken.None);

                            if (_selectedServerId == serverId)
                            {
                                await ReloadParticipantsAsync(serverId);
                            }

                            ParticipantCheckCompleted?.Invoke(this, EventArgs.Empty);

                            var updatedServer = await _worldHubServerService.GetByIdAsync(serverId);
                            var friendName = updatedServer?.Participants
                                .FirstOrDefault(p => p.Id == participant.Id)?.UserName;

                            var friendDisplay = !string.IsNullOrWhiteSpace(friendName)
                                ? friendName
                                : targetIp;

                            DialogWindow.ShowInformation(
                                owner,
                                "Приглашение принято",
                                $"{friendDisplay} присоединился к серверу «{serverName}».");
                        }
                        catch (Exception exception)
                        {
                            AppLog.Error($"Failed to finalize accepted participant: {exception.Message}", exception);
                        }
                    }
                    else
                    {
                        DialogWindow.ShowInformation(
                            owner,
                            "Приглашение отклонено",
                            $"Друг ({targetIp}) отклонил приглашение.");
                    }
                });
            }
            catch (TimeoutException)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    DialogWindow.ShowWarning(
                        owner,
                        "Друг недоступен",
                        $"Друг ({targetIp}) не ответил на приглашение за 60 секунд. Попробуйте позже.");
                });
            }
            catch (Exception exception)
            {
                AppLog.Warning($"Invite to {targetIp} failed: {exception.Message}");

                await Dispatcher.InvokeAsync(() =>
                {
                    DialogWindow.ShowWarning(
                        owner,
                        "Друг недоступен",
                        $"Не удалось связаться с {targetIp}.\nУбедитесь, что Radmin VPN включён и WorldHub запущен.");
                });
            }
        });
    }

    private async void CheckParticipantButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedServerId is null ||
            _checkService is null ||
            sender is not Button button ||
            button.Tag is not WorldHubParticipantViewModel vm)
        {
            return;
        }

        AppLog.Separator($"Manual check of '{vm.DisplayName}'");

        var serverId = _selectedServerId.Value;
        button.IsEnabled = false;

        try
        {
            await _checkService.CheckAsync(
                serverId,
                vm.Id,
                CancellationToken.None);

            if (_selectedServerId == serverId)
            {
                await ReloadParticipantsAsync(serverId);
            }

            ParticipantCheckCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                $"Не удалось проверить участника.\n\n{exception.Message}",
                "WorldHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void RemoveParticipantButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedServerId is null ||
            _checkService is null ||
            sender is not Button button ||
            button.Tag is not WorldHubParticipantViewModel vm)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        var message = string.IsNullOrWhiteSpace(_selectedServerName)
            ? $"Удалить участника «{vm.DisplayName}» из WorldHub-сервера?"
            : $"Удалить участника «{vm.DisplayName}» из WorldHub-сервера «{_selectedServerName}»?";

        var confirmed = DialogWindow.ShowConfirmation(
            owner,
            "Удаление участника",
            message);

        if (!confirmed)
        {
            return;
        }

        var serverId = _selectedServerId.Value;
        button.IsEnabled = false;

        try
        {
            await _checkService.RemoveParticipantAsync(
                serverId,
                vm.Id,
                CancellationToken.None);

            if (_selectedServerId == serverId)
            {
                await ReloadParticipantsAsync(serverId);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            DialogWindow.ShowError(
                owner,
                "Ошибка удаления",
                $"Не удалось удалить участника.\n\n{exception.Message}");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
