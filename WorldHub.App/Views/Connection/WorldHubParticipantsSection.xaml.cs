using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services.Network;
using WorldHub.App.Views.Dialogs;
using WorldHub.Core.Entities;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Connection;

public partial class WorldHubParticipantsSection : UserControl
{
    private WorldHubParticipantCheckService? _checkService;
    private WorldHubServerService? _worldHubServerService;
    private string _localDeviceId = string.Empty;
    private Guid? _selectedServerId;
    private string? _selectedServerName;
    private bool _isChecking;

    public ObservableCollection<WorldHubParticipantViewModel> Participants { get; } = new();

    public event Func<WorldHubParticipantViewModel, Task>? InviteRequested;
    public event EventHandler<int>? ParticipantCountChanged;

    public WorldHubParticipantsSection()
    {
        InitializeComponent();
        ParticipantsItemsControl.ItemsSource = Participants;
    }

    public void Initialize(
        WorldHubParticipantCheckService checkService,
        WorldHubServerService worldHubServerService,
        string localDeviceId)
    {
        ArgumentNullException.ThrowIfNull(checkService);
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDeviceId);

        _checkService = checkService;
        _worldHubServerService = worldHubServerService;
        _localDeviceId = localDeviceId;
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
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"CheckAll failed: {exception.Message}");
        }
        finally
        {
            _isChecking = false;
        }
    }

    public async Task AddParticipantAsync(Window owner)
    {
        if (_selectedServerId is null || _checkService is null)
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

        try
        {
            var participant = await _checkService.AddParticipantAsync(
                serverId,
                window.IpAddress!,
                CancellationToken.None);

            if (_selectedServerId == serverId)
            {
                await ReloadParticipantsAsync(serverId);
            }

            await _checkService.CheckAsync(
                serverId,
                participant.Id,
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
            MessageBox.Show(
                owner,
                exception.Message,
                "Не удалось добавить участника",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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

    private async void InviteParticipantButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not WorldHubParticipantViewModel vm)
        {
            return;
        }

        button.IsEnabled = false;
        button.Content = "...";

        try
        {
            if (InviteRequested is not null)
            {
                await InviteRequested.Invoke(vm);
            }
        }
        finally
        {
            button.IsEnabled = true;
            button.Content = "Пригласить";
        }
    }
}
