using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WorldHub.App.Services.Network;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Infrastructure.Google;
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
    private readonly string? _localDeviceId;
    private readonly GoogleDriveClient? _googleDriveClient;
    private readonly Services.Settings.AppSettingsService? _appSettingsService;
    private readonly WorldSyncService? _worldSyncService;

    private bool _loadingWorldHubServers;
    private string? _currentFolderId;

    public event EventHandler? BackRequested;

    public MinecraftServerSettingsView(
        Server server,
        ServerService serverService,
        WorldHubServerService worldHubServerService,
        DedicatedServerProcessManager processManager,
        GoogleDriveStatusCache? googleDriveStatusCache = null,
        string? localDeviceId = null,
        GoogleDriveClient? googleDriveClient = null,
        Services.Settings.AppSettingsService? appSettingsService = null)
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
        _localDeviceId = localDeviceId;
        _googleDriveClient = googleDriveClient;
        _appSettingsService = appSettingsService;

        if (_googleDriveClient is not null)
        {
            _worldSyncService = new WorldSyncService(_googleDriveClient);
        }

        InitializeComponent();

        if (_server.IsImportedFromWorldHub)
        {
            WorldHubServerComboBox.IsEnabled = false;
            SaveWorldHubServerButton.Visibility = Visibility.Collapsed;
        }

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

            var allServers =
                await _worldHubServerService.GetAllAsync();

            // К WorldHub-серверу может привязать Minecraft-сервер только хост (создатель).
            // Фильтруем серверы: показываем только те, где локальный пользователь является хостом.
            IReadOnlyList<WorldHubServer> selectableServers;

            if (_server.IsImportedFromWorldHub && _server.WorldHubServerId is Guid importedId)
            {
                // Импортированный Minecraft-сервер: показываем только тот WorldHub-сервер,
                // к которому он привязан, независимо от того, хост ли локальный пользователь.
                var imported = allServers.FirstOrDefault(s => s.Id == importedId);
                selectableServers = imported is not null
                    ? new List<WorldHubServer> { imported }
                    : new List<WorldHubServer>();
            }
            else if (!string.IsNullOrWhiteSpace(_localDeviceId))
            {
                selectableServers = allServers.Where(s =>
                {
                    var firstParticipant = s.Participants.FirstOrDefault();
                    if (firstParticipant is not null && !string.IsNullOrWhiteSpace(firstParticipant.DeviceId))
                    {
                        return string.Equals(firstParticipant.DeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase);
                    }
                    if (!string.IsNullOrWhiteSpace(s.HostDeviceId))
                    {
                        return string.Equals(s.HostDeviceId, _localDeviceId, StringComparison.OrdinalIgnoreCase);
                    }
                    return true;
                }).ToList();
            }
            else
            {
                selectableServers = allServers;
            }

            WorldHubServerComboBox.ItemsSource = selectableServers;

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

        if (_server.IsImportedFromWorldHub)
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

    private async void SaveWorldHubServerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_server.IsImportedFromWorldHub)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        Guid? worldHubServerId = WorldHubServerComboBox.SelectedValue as Guid?;

        try
        {
            _server.WorldHubServerId = worldHubServerId;
            await _serverService.UpdateAsync(_server);
            await RefreshCloudBlockAsync();

            AppLog.Success($"[SERVER] Привязка сервера «{_server.Name}» сохранена: WorldHubServerId={worldHubServerId}.");

            if (owner is not null)
            {
                Views.Dialogs.DialogWindow.ShowInformation(
                    owner,
                    "Сохранено",
                    worldHubServerId.HasValue
                        ? "Minecraft-сервер успешно привязан к выбранному WorldHub-серверу."
                        : "Привязка к WorldHub-серверу отключена.");
            }
        }
        catch (Exception exception)
        {
            AppLog.Error($"Ошибка сохранения привязки WorldHub-сервера: {exception.Message}", exception);
            if (owner is not null)
            {
                Views.Dialogs.DialogWindow.ShowError(
                    owner,
                    "Ошибка сохранения",
                    $"Не удалось сохранить привязку:\n{exception.Message}");
            }
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

    private async void DeleteServerButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        var status = _processManager.GetStatus(_server.Id);
        if (status == ServerStatus.Running)
        {
            Views.Dialogs.DialogWindow.ShowWarning(
                owner,
                "Сервер работает",
                $"Нельзя удалить сервер «{_server.Name}», пока он запущен.\nОстановите сервер перед удалением.");
            return;
        }

        var confirmed = Views.Dialogs.DialogWindow.ShowConfirmation(
            owner,
            "Удаление сервера",
            $"Вы действительно хотите удалить сервер «{_server.Name}»?\n\n" +
            "• Сервер будет удалён только из программы WorldHub.\n" +
            "• Файлы сервера на вашем диске останутся нетронутыми:\n" +
            $"  {_server.LocalPath}\n\n" +
            "Продолжить?");

        if (!confirmed)
        {
            return;
        }

        DeleteServerButton.IsEnabled = false;

        try
        {
            await _serverService.DeleteAsync(_server.Id);

            AppLog.Success($"[SERVER] Сервер «{_server.Name}» удалён из программы.");

            BackRequested?.Invoke(
                this,
                EventArgs.Empty);
        }
        catch (Exception exception)
        {
            AppLog.Error($"Не удалось удалить сервер «{_server.Name}»: {exception.Message}", exception);

            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Ошибка удаления",
                $"Не удалось удалить сервер.\n\n{exception.Message}");

            DeleteServerButton.IsEnabled = true;
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

    private async void PushButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        // 1. Проверяем статус работы сервера
        var status = _processManager.GetStatus(_server.Id);
        if (status == ServerStatus.Running)
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Сервер запущен",
                "Невозможно отправить сервер в облако (Push), пока сервер запущен.\nОстановите сервер перед синхронизацией.");
            return;
        }

        // 2. Проверяем привязку к WorldHub-серверу
        if (_server.WorldHubServerId is null)
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Нет привязки к WorldHub-серверу",
                "Этот Minecraft-сервер не привязан к WorldHub-серверу.\nВыберите сервер в блоке «Облако» выше.");
            return;
        }

        var worldHubServer = await _worldHubServerService.GetByIdAsync(_server.WorldHubServerId.Value);
        if (worldHubServer is null || string.IsNullOrWhiteSpace(worldHubServer.GoogleDriveFolderId))
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Общая папка не найдена",
                "У привязанного WorldHub-сервера ещё не создана общая папка Google Drive.\nСоздайте её в разделе «Подключение».");
            return;
        }

        if (_worldSyncService is null)
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Google Drive не подключен",
                "Служба Google Drive недоступна. Проверьте авторизацию в Настройках.");
            return;
        }

        // 3. Проверяем наличие папки сервера
        if (!Directory.Exists(_server.LocalPath))
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Папка сервера не найдена",
                $"Папка сервера не найдена по пути:\n{_server.LocalPath}");
            return;
        }

        // 4. Запрашиваем подтверждение
        var confirmed = Views.Dialogs.DialogWindow.ShowConfirmation(
            owner,
            "Отправка сервера в облако (Push)",
            $"Вы собираетесь выгрузить весь сервер «{_server.Name}» в общую папку «{worldHubServer.Name}».\n\n" +
            "• Все файлы сервера (ядро, конфиги, плагины/моды, миры) будут упакованы и отправлены на Google Drive.\n" +
            "• Друзья смогут получить актуальную копию всего сервера (Pull).\n\n" +
            "Продолжить?");

        if (!confirmed)
        {
            return;
        }

        PushButton.IsEnabled = false;
        PullButton.IsEnabled = false;

        var progressWindow = new Views.Dialogs.SyncProgressWindow(
            "Отправка сервера (Push)",
            $"Выгрузка сервера «{_server.Name}» в облако...",
            "/Assets/Icons/upload.svg",
            owner);

        progressWindow.Show();

        try
        {
            var userName = _appSettingsService?.GetUserName() ?? Environment.UserName;
            var deviceId = _localDeviceId ?? string.Empty;

            var progress = new Progress<double>(p => progressWindow.ReportProgress(p));
            var statusMsg = new Progress<string>(s => progressWindow.ReportStatus(s));

            await _worldSyncService.PushServerAsync(
                worldHubServer.GoogleDriveFolderId,
                _server.LocalPath,
                _server.Name,
                _server.MinecraftVersion,
                _server.Loader,
                _server.LoaderVersion,
                userName,
                deviceId,
                progress,
                statusMsg);

            progressWindow.Complete("Сервер успешно выгружен в Google Drive! Участники могут скачать его.");
        }
        catch (Exception exception)
        {
            AppLog.Error($"[SYNC] Ошибка Push: {exception.Message}", exception);
            progressWindow.Complete($"Ошибка выгрузки сервера: {exception.Message}", isSuccess: false);
        }
        finally
        {
            PushButton.IsEnabled = true;
            PullButton.IsEnabled = true;
        }
    }

    private async void PullButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        // 1. Проверяем статус работы сервера
        var status = _processManager.GetStatus(_server.Id);
        if (status == ServerStatus.Running)
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Сервер запущен",
                "Невозможно обновить сервер из облака (Pull), пока сервер запущен.\nОстановите сервер перед синхронизацией.");
            return;
        }

        // 2. Проверяем привязку к WorldHub-серверу
        if (_server.WorldHubServerId is null)
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Нет привязки к WorldHub-серверу",
                "Этот Minecraft-сервер не привязан к WorldHub-серверу.\nВыберите сервер в блоке «Облако» выше.");
            return;
        }

        var worldHubServer = await _worldHubServerService.GetByIdAsync(_server.WorldHubServerId.Value);
        if (worldHubServer is null || string.IsNullOrWhiteSpace(worldHubServer.GoogleDriveFolderId))
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Общая папка не найдена",
                "У привязанного WorldHub-сервера ещё не создана общая папка Google Drive.\nСоздайте её в разделе «Подключение».");
            return;
        }

        if (_worldSyncService is null)
        {
            Views.Dialogs.DialogWindow.ShowError(
                owner,
                "Google Drive не подключен",
                "Служба Google Drive недоступна. Проверьте авторизацию в Настройках.");
            return;
        }

        // 3. Запрашиваем подтверждение
        var confirmed = Views.Dialogs.DialogWindow.ShowConfirmation(
            owner,
            "Загрузка сервера из облака (Pull)",
            $"Вы собираетесь скачать сервер из общей папки «{worldHubServer.Name}».\n\n" +
            $"Целевая папка на вашем диске:\n{_server.LocalPath}\n\n" +
            "• Перед заменой будет автоматически создан бэкап вашей текущей папки сервера.\n" +
            "• Сервер будет обновлен до версии из Google Drive.\n\n" +
            "Продолжить?");

        if (!confirmed)
        {
            return;
        }

        PushButton.IsEnabled = false;
        PullButton.IsEnabled = false;

        var progressWindow = new Views.Dialogs.SyncProgressWindow(
            "Загрузка сервера (Pull)",
            $"Скачивание сервера из общей папки...",
            "/Assets/Icons/download.svg",
            owner);

        progressWindow.Show();

        try
        {
            var progress = new Progress<double>(p => progressWindow.ReportProgress(p));
            var statusMsg = new Progress<string>(s => progressWindow.ReportStatus(s));

            await _worldSyncService.PullServerAsync(
                worldHubServer.GoogleDriveFolderId,
                _server.LocalPath,
                progress,
                statusMsg);

            progressWindow.Complete("Сервер успешно скачан и установлен из Google Drive!");
        }
        catch (Exception exception)
        {
            AppLog.Error($"[SYNC] Ошибка Pull: {exception.Message}", exception);
            progressWindow.Complete($"Ошибка загрузки сервера: {exception.Message}", isSuccess: false);
        }
        finally
        {
            PushButton.IsEnabled = true;
            PullButton.IsEnabled = true;
        }
    }

    private static string GetWorldName(string serverDirectory)
    {
        try
        {
            var propsFile = Path.Combine(serverDirectory, "server.properties");
            if (File.Exists(propsFile))
            {
                foreach (var line in File.ReadLines(propsFile))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("level-name=", StringComparison.OrdinalIgnoreCase))
                    {
                        var val = trimmed["level-name=".Length..].Trim();
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            return val;
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return "world";
    }
}