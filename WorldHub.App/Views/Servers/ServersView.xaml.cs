using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WorldHub.App.Views.Main;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;
using WorldHub.Infrastructure.Minecraft;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Servers;

public partial class ServersView : UserControl
{
    private readonly ServerService _serverService;
    private readonly IServerDetector _serverDetector;
    private readonly DedicatedServerProcessManager _processManager;

    private readonly DispatcherTimer _statusTimer;

    public ObservableCollection<ServerCardViewModel> Servers { get; } = new();

    public ServersView(
        ServerService serverService,
        IServerDetector serverDetector,
        DedicatedServerProcessManager processManager)
    {
        ArgumentNullException.ThrowIfNull(serverService);
        ArgumentNullException.ThrowIfNull(serverDetector);
        ArgumentNullException.ThrowIfNull(processManager);

        _serverService = serverService;
        _serverDetector = serverDetector;
        _processManager = processManager;

        InitializeComponent();

        ServersItemsControl.ItemsSource = Servers;

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _statusTimer.Tick += StatusTimer_Tick;

        Loaded += ServersView_Loaded;
        Unloaded += ServersView_Unloaded;
    }

    private async void ServersView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadServersAsync();

        _statusTimer.Start();
    }

    private void ServersView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _statusTimer.Stop();
    }

    private async void StatusTimer_Tick(
        object? sender,
        EventArgs e)
    {
        await RefreshStatusesAsync();
    }

    private async void AddServerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var window =
            new AddServerWindow(
                _serverService,
                _serverDetector)
            {
                Owner = Window.GetWindow(this)
            };

        var result = window.ShowDialog();

        if (result == true)
        {
            await LoadServersAsync();
        }
    }

    private async void ControlButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ServerCardViewModel card)
        {
            return;
        }

        card.IsBusy = true;

        try
        {
            if (card.Status is
                ServerStatus.Stopped or
                ServerStatus.Error)
            {
                card.Status = ServerStatus.Starting;

                await _processManager.StartAsync(
                    card.Server);
            }
            else if (card.Status == ServerStatus.Running)
            {
                card.Status = ServerStatus.Stopping;

                await _processManager.StopAsync(
                    card.Server);
            }
        }
        catch (Exception exception)
        {
            card.Status = ServerStatus.Error;

            MessageBox.Show(
                Window.GetWindow(this),
                exception.Message,
                "Ошибка управления сервером",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            card.IsBusy = false;

            await RefreshStatusesAsync();
        }
    }

    private async Task LoadServersAsync()
    {
        try
        {
            var servers =
                await _serverService.GetAllAsync();

            Servers.Clear();

            foreach (var server in servers)
            {
                var card =
                    new ServerCardViewModel(server)
                    {
                        Status =
                            _processManager.GetStatus(server.Id)
                    };

                Servers.Add(card);
            }

            var hasServers = Servers.Count > 0;

            EmptyState.Visibility =
                hasServers
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            ServersList.Visibility =
                hasServers
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            HeaderAddServerButton.Visibility =
                hasServers
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                exception.Message,
                "Не удалось загрузить серверы",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenServerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.Tag is not ServerCardViewModel card)
        {
            return;
        }

        var window = Window.GetWindow(this);

        if (window is MainWindow mainWindow)
        {
            mainWindow.OpenServerSettings(card.Server);
        }
    }

    private Task RefreshStatusesAsync()
    {
        foreach (var card in Servers)
        {
            if (card.IsBusy)
            {
                continue;
            }

            card.Status =
                _processManager.GetStatus(card.Id);
        }

        return Task.CompletedTask;
    }
}