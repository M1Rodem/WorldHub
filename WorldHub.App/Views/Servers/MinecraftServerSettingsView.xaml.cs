using System.Windows;
using System.Windows.Controls;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Infrastructure.Minecraft;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Servers;

public partial class MinecraftServerSettingsView : UserControl
{
    private readonly Server _server;
    private readonly ServerService _serverService;
    private readonly WorldHubServerService _worldHubServerService;
    private readonly DedicatedServerProcessManager _processManager;

    private bool _loadingWorldHubServers;

    public event EventHandler? BackRequested;

    public MinecraftServerSettingsView(
        Server server,
        ServerService serverService,
        WorldHubServerService worldHubServerService,
        DedicatedServerProcessManager processManager)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(serverService);
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(processManager);

        _server = server;
        _serverService = serverService;
        _worldHubServerService = worldHubServerService;
        _processManager = processManager;

        InitializeComponent();

        ServerNameTextBlock.Text = server.Name;

        ServerPathTextBlock.Text =
            server.LocalPath;

        MinecraftVersionTextBlock.Text =
            server.MinecraftVersion;

        LoaderTextBlock.Text =
            server.Loader;

        LoaderVersionTextBlock.Text =
            server.LoaderVersion ?? "—";

        RefreshStatus();

        _ = LoadWorldHubServersAsync();
    }

    private async Task LoadWorldHubServersAsync()
    {
        try
        {
            _loadingWorldHubServers = true;

            var servers =
                await _worldHubServerService.GetAllAsync();

            WorldHubServerComboBox.ItemsSource = servers;

            if (_server.WorldHubServerId is Guid worldHubServerId)
            {
                WorldHubServerComboBox.SelectedValue =
                    worldHubServerId;
            }
            else
            {
                WorldHubServerComboBox.SelectedIndex = -1;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                $"Не удалось загрузить WorldHub-серверы.\n\n{exception.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _loadingWorldHubServers = false;
        }
    }

    private async void WorldHubServerComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_loadingWorldHubServers)
        {
            return;
        }

        if (WorldHubServerComboBox.SelectedValue is not Guid worldHubServerId)
        {
            return;
        }

        if (_server.WorldHubServerId == worldHubServerId)
        {
            return;
        }

        try
        {
            _server.WorldHubServerId =
                worldHubServerId;

            await _serverService.UpdateAsync(_server);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                $"Не удалось сохранить WorldHub-сервер.\n\n{exception.Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            await LoadWorldHubServersAsync();
        }
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        BackRequested?.Invoke(
            this,
            EventArgs.Empty);
    }

    private async void ServerActionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ServerActionButton.IsEnabled = false;

        try
        {
            var status =
                _processManager.GetStatus(
                    _server.Id);

            if (status is
                ServerStatus.Stopped or
                ServerStatus.Error)
            {
                await _processManager.StartAsync(
                    _server);
            }
            else if (status ==
                     ServerStatus.Running)
            {
                await _processManager.StopAsync(
                    _server);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                exception.Message,
                "Ошибка управления сервером",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            RefreshStatus();
            ServerActionButton.IsEnabled = true;
        }
    }

    private void RefreshStatus()
    {
        var status = _processManager.GetStatus(_server.Id);

        StatusTextBlock.Text = ServerStatusUiHelper.GetStatusText(status);
        StatusIndicator.Fill = ServerStatusUiHelper.GetStatusBrush(status);
        ServerActionButton.Content = ServerStatusUiHelper.GetActionText(status);
        ServerActionButton.IsEnabled = ServerStatusUiHelper.CanExecuteAction(status);

        if (status == ServerStatus.Running)
        {
            if (TryFindResource("DangerButton") is Style dangerStyle)
            {
                ServerActionButton.Style = dangerStyle;
            }
        }
        else
        {
            if (TryFindResource("PrimaryButton") is Style primaryStyle)
            {
                ServerActionButton.Style = primaryStyle;
            }
        }
    }
}