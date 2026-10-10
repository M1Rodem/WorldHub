using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WorldHub.App.Services.Network;
using WorldHub.App.Views.Dialogs;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;
using WorldHub.Logging;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Servers;

public partial class DownloadServerFromWorldHubWindow : Window
{
    private readonly WorldHubServerService _worldHubServerService;
    private readonly WorldSyncService _worldSyncService;
    private readonly IServerDetector _serverDetector;
    private readonly ServerService _serverService;
    private readonly GoogleDriveStatusCache? _googleDriveStatusCache;

    private ServerPackageSyncStatus? _currentCloudStatus;
    private WorldHubServer? _selectedWorldHubServer;

    public DownloadServerFromWorldHubWindow(
        WorldHubServerService worldHubServerService,
        WorldSyncService worldSyncService,
        IServerDetector serverDetector,
        ServerService serverService,
        GoogleDriveStatusCache? googleDriveStatusCache = null)
    {
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(worldSyncService);
        ArgumentNullException.ThrowIfNull(serverDetector);
        ArgumentNullException.ThrowIfNull(serverService);

        _worldHubServerService = worldHubServerService;
        _worldSyncService = worldSyncService;
        _serverDetector = serverDetector;
        _serverService = serverService;
        _googleDriveStatusCache = googleDriveStatusCache;

        InitializeComponent();

        Loaded += DownloadServerFromWorldHubWindow_Loaded;
    }

    private async void DownloadServerFromWorldHubWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        await LoadWorldHubServersAsync();
    }

    private async Task LoadWorldHubServersAsync()
    {
        try
        {
            StatusMessageTextBlock.Text = "Загрузка списка WorldHub-серверов...";
            var servers = await _worldHubServerService.GetAllAsync();

            if (servers.Count == 0)
            {
                StatusMessageTextBlock.Text =
                    "Вы пока не состоите ни в одном WorldHub-сервере.\nСоздайте сервер или примите приглашение от друга в разделе «Подключение».";
                WorldHubServerComboBox.ItemsSource = null;
                return;
            }

            WorldHubServerComboBox.ItemsSource = servers;
            WorldHubServerComboBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Ошибка загрузки WorldHub-серверов: {ex.Message}", ex);
            StatusMessageTextBlock.Text = $"Не удалось загрузить WorldHub-серверы: {ex.Message}";
        }
    }

    private async void WorldHubServerComboBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        _selectedWorldHubServer = WorldHubServerComboBox.SelectedItem as WorldHubServer;
        _currentCloudStatus = null;

        ServerInfoCard.Visibility = Visibility.Collapsed;
        DestinationFolderPanel.Visibility = Visibility.Collapsed;
        DownloadButton.IsEnabled = false;

        if (_selectedWorldHubServer is null)
        {
            StatusMessageTextBlock.Text = "Выберите WorldHub-сервер из списка выше.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedWorldHubServer.GoogleDriveFolderId))
        {
            StatusMessageTextBlock.Text =
                $"У сервера «{_selectedWorldHubServer.Name}» ещё не создана общая папка Google Drive.\nХост сервера должен создать общую папку в разделе «Подключение».";
            return;
        }

        try
        {
            StatusMessageTextBlock.Text = "Проверка наличия сервера в Google Drive...";
            var cloudStatus = await _worldSyncService.CheckCloudServerStatusAsync(_selectedWorldHubServer.GoogleDriveFolderId);
            _currentCloudStatus = cloudStatus;

            if (!cloudStatus.HasCloudPackage || cloudStatus.Manifest is null)
            {
                StatusMessageTextBlock.Text =
                    $"В общей папке сервера «{_selectedWorldHubServer.Name}» пока нет выгруженного Minecraft-сервера.\nХост должен выполнить «Push» в настройках своего сервера.";
                return;
            }

            var manifest = cloudStatus.Manifest;
            StatusMessageTextBlock.Text = "Готово к загрузке. Укажите папку для установки на вашем компьютере.";

            ServerNameTextBlock.Text = manifest.ServerName;
            MinecraftVersionTextBlock.Text = manifest.MinecraftVersion;
            LoaderTextBlock.Text = string.IsNullOrWhiteSpace(manifest.LoaderVersion)
                ? manifest.Loader ?? "Vanilla"
                : $"{manifest.Loader} ({manifest.LoaderVersion})";

            PushedByTextBlock.Text = $"{manifest.PushedByUserName} ({manifest.PushedAtUtc.ToLocalTime():g})";
            ArchiveSizeTextBlock.Text = $"{manifest.ArchiveSizeBytes / (1024.0 * 1024.0):F1} МБ";

            var defaultFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "MinecraftServers",
                manifest.ServerName);

            DestinationPathTextBox.Text = defaultFolder;

            ServerInfoCard.Visibility = Visibility.Visible;
            DestinationFolderPanel.Visibility = Visibility.Visible;
            DownloadButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AppLog.Error($"Ошибка проверки статуса сервера в облаке: {ex.Message}", ex);
            StatusMessageTextBlock.Text = $"Ошибка проверки сервера в Google Drive: {ex.Message}";
        }
    }

    private void BrowseFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку для установки Minecraft-сервера",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(DestinationPathTextBox.Text))
        {
            try
            {
                var parent = Path.GetDirectoryName(DestinationPathTextBox.Text);
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    dialog.InitialDirectory = parent;
                }
            }
            catch
            {
            }
        }

        if (dialog.ShowDialog(this) == true)
        {
            DestinationPathTextBox.Text = dialog.FolderName;
        }
    }

    private async void DownloadButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedWorldHubServer is null ||
            _currentCloudStatus is null ||
            !_currentCloudStatus.HasCloudPackage ||
            _currentCloudStatus.Manifest is null ||
            string.IsNullOrWhiteSpace(_selectedWorldHubServer.GoogleDriveFolderId))
        {
            DialogWindow.ShowWarning(this, "Недоступно", "Нет доступного пакета для скачивания.");
            return;
        }

        var destinationDir = DestinationPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(destinationDir))
        {
            DialogWindow.ShowWarning(this, "Укажите папку", "Пожалуйста, укажите папку для установки сервера.");
            return;
        }

        try
        {
            destinationDir = Path.GetFullPath(destinationDir);
        }
        catch (Exception ex)
        {
            DialogWindow.ShowError(this, "Некорректный путь", $"Указан недопустимый путь к папке:\n{ex.Message}");
            return;
        }

        if (Directory.Exists(destinationDir) && Directory.EnumerateFileSystemEntries(destinationDir).Any())
        {
            var confirmOverwrite = DialogWindow.ShowConfirmation(
                this,
                "Папка не пуста",
                $"Папка «{destinationDir}» уже содержит файлы.\n\nПеред распаковкой будет создан бэкап, но существующие файлы могут быть перезаписаны.\nПродолжить?");

            if (!confirmOverwrite)
            {
                return;
            }
        }

        var manifest = _currentCloudStatus.Manifest;
        var folderId = _selectedWorldHubServer.GoogleDriveFolderId;

        DownloadButton.IsEnabled = false;

        var progressWindow = new SyncProgressWindow(
            "Скачивание сервера",
            $"Загрузка «{manifest.ServerName}» из Google Drive...",
            "/Assets/Icons/download.svg",
            this);

        progressWindow.Show();

        try
        {
            var progress = new Progress<double>(p => progressWindow.ReportProgress(p));
            var statusMsg = new Progress<string>(s => progressWindow.ReportStatus(s));

            await _worldSyncService.PullServerAsync(
                folderId,
                destinationDir,
                progress,
                statusMsg);

            progressWindow.Complete("Сервер успешно скачан и распакован!");

            // Определяем файл запуска
            var launchFilePath = FindLaunchFile(destinationDir);
            ServerDetectionResult? detection = null;

            if (launchFilePath is not null)
            {
                try
                {
                    detection = _serverDetector.Detect(launchFilePath);
                }
                catch (Exception ex)
                {
                    AppLog.Warning($"[SYNC] Не удалось автодетектировать файл запуска: {ex.Message}");
                }
            }

            var relativeLaunchFile = launchFilePath is not null
                ? Path.GetRelativePath(destinationDir, launchFilePath)
                : "run.bat";

            var serverLaunchType = ServerLaunchType.Script;
            if (relativeLaunchFile.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            {
                serverLaunchType = ServerLaunchType.Jar;
            }

            detection ??= new ServerDetectionResult
            {
                ServerDirectory = destinationDir,
                MinecraftVersion = manifest.MinecraftVersion,
                Loader = manifest.Loader ?? "Vanilla",
                LoaderVersion = manifest.LoaderVersion,
                LaunchConfiguration = new ServerLaunchConfiguration
                {
                    Type = serverLaunchType,
                    FilePath = relativeLaunchFile
                }
            };

            var createdServer = await _serverService.CreateAsync(manifest.ServerName, detection);
            createdServer.WorldHubServerId = _selectedWorldHubServer.Id;
            await _serverService.UpdateAsync(createdServer);

            AppLog.Success($"[SERVER] Сервер «{createdServer.Name}» успешно добавлен и привязан к WorldHub «{_selectedWorldHubServer.Name}».");

            DialogWindow.ShowInformation(
                this,
                "Сервер установлен",
                $"Minecraft-сервер «{createdServer.Name}» успешно установлен и добавлен в список ваших серверов!\n\nПуть: {destinationDir}");

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            AppLog.Error($"[SYNC] Ошибка скачивания сервера: {ex.Message}", ex);
            progressWindow.Complete($"Ошибка загрузки сервера: {ex.Message}", isSuccess: false);
            DownloadButton.IsEnabled = true;
        }
    }

    private static string? FindLaunchFile(string serverDirectory)
    {
        if (!Directory.Exists(serverDirectory))
        {
            return null;
        }

        var commonNames = new[]
        {
            "run.bat",
            "start.bat",
            "launch.bat",
            "server.bat",
            "run.cmd",
            "start.cmd",
            "server.jar"
        };

        foreach (var name in commonNames)
        {
            var path = Path.Combine(serverDirectory, name);
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Поиск любого .bat или .cmd в корне
        var batchFiles = Directory.GetFiles(serverDirectory, "*.bat")
            .Concat(Directory.GetFiles(serverDirectory, "*.cmd"))
            .ToList();

        if (batchFiles.Count > 0)
        {
            return batchFiles[0];
        }

        // Поиск .jar файлов в корне
        var jarFiles = Directory.GetFiles(serverDirectory, "*.jar");
        return jarFiles.FirstOrDefault();
    }

    private void CancelButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
