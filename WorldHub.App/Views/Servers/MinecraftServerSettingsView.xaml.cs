using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WorldHub.App.Services.Network;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Infrastructure.Minecraft;
using WorldHub.Logging;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Servers;

public partial class MinecraftServerSettingsView : UserControl
{
    private readonly Server _server;
    private readonly ServerService _serverService;
    private readonly WorldHubServerService _worldHubServerService;
    private readonly DedicatedServerProcessManager _processManager;
    private readonly GoogleDriveStatusCache? _googleDriveStatusCache;

    private bool _loadingWorldHubServers;
    private string? _currentFolderId;

    public event EventHandler? BackRequested;

    public MinecraftServerSettingsView(
        Server server,
        ServerService serverService,
        WorldHubServerService worldHubServerService,
        DedicatedServerProcessManager processManager,
        GoogleDriveStatusCache? googleDriveStatusCache = null)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(serverService);
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(processManager);

        _server = server;
        _serverService = serverService;
        _worldHubServerService = worldHubServerService;
        _processManager = processManager;
        _googleDriveStatusCache = googleDriveStatusCache;

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
        _ = RefreshCloudBlockAsync();
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

        Guid? worldHubServerId = WorldHubServerComboBox.SelectedValue as Guid?;

        if (_server.WorldHubServerId == worldHubServerId)
        {
            return;
        }

        try
        {
            _server.WorldHubServerId =
                worldHubServerId;

            await _serverService.UpdateAsync(_server);
            await RefreshCloudBlockAsync();
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

    private async Task RefreshCloudBlockAsync()
    {
        var isGoogleConnected = string.Equals(
            _googleDriveStatusCache?.GetStatusString(),
            "DriveAvailable",
            StringComparison.OrdinalIgnoreCase);

        CloudStatusIcon.Source = new Uri(
            isGoogleConnected
                ? "/Assets/Icons/google-on.svg"
                : "/Assets/Icons/google-off.svg",
            UriKind.Relative);

        CloudStatusDot.Fill = isGoogleConnected
            ? new SolidColorBrush(Color.FromRgb(104, 211, 145))
            : (Brush)FindResource("TextMutedBrush");

        if (_server.WorldHubServerId is null)
        {
            CloudStatusTextBlock.Text = "Не привязан к WorldHub-серверу";
            CloudFolderHintTextBlock.Visibility = Visibility.Collapsed;
            OpenCloudFolderButton.IsEnabled = false;
            _currentFolderId = null;
            return;
        }

        var worldHubServer = await _worldHubServerService.GetByIdAsync(_server.WorldHubServerId.Value);

        if (worldHubServer is null)
        {
            CloudStatusTextBlock.Text = "Не привязан к WorldHub-серверу";
            CloudFolderHintTextBlock.Visibility = Visibility.Collapsed;
            OpenCloudFolderButton.IsEnabled = false;
            _currentFolderId = null;
            return;
        }

        if (string.IsNullOrWhiteSpace(worldHubServer.GoogleDriveFolderId))
        {
            CloudStatusTextBlock.Text = "Общая папка не создана";
            CloudFolderHintTextBlock.Visibility = Visibility.Collapsed;
            OpenCloudFolderButton.IsEnabled = false;
            _currentFolderId = null;
        }
        else
        {
            CloudStatusTextBlock.Text = $"Общая папка WorldHub-сервера {worldHubServer.Name}";
            CloudFolderHintTextBlock.Text = $"Откроется общая папка WorldHub-сервера {worldHubServer.Name}";
            CloudFolderHintTextBlock.Visibility = Visibility.Visible;
            OpenCloudFolderButton.IsEnabled = true;
            _currentFolderId = worldHubServer.GoogleDriveFolderId;
        }
    }

    private void OpenCloudFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFolderId))
        {
            return;
        }

        try
        {
            var url = $"https://drive.google.com/drive/folders/{_currentFolderId}";
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            AppLog.Error($"Не удалось открыть папку Google Drive: {exception.Message}", exception);
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