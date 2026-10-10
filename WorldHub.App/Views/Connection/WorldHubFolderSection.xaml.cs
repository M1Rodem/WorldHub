using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WorldHub.App.Services.Network;
using WorldHub.Core.Entities;
using WorldHub.Infrastructure.Google;
using WorldHub.Logging;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Connection;

public partial class WorldHubFolderSection : UserControl
{
    private static readonly Brush OfflineBrush =
        new SolidColorBrush(Color.FromRgb(0xE5, 0x6B, 0x6F));

    private static readonly Brush UnknownBrush =
        new SolidColorBrush(Color.FromRgb(0x8C, 0x95, 0xA3));

    private GoogleDriveClient? _googleDriveClient;
    private WorldHubServerService? _worldHubServerService;
    private WorldHubFolderSharingService? _sharingService;
    private WorldHubServer? _currentServer;

    public event EventHandler<WorldHubServer>? ServerUpdated;

    public WorldHubFolderSection()
    {
        InitializeComponent();
    }

    public void Initialize(
        GoogleDriveClient googleDriveClient,
        WorldHubServerService worldHubServerService,
        WorldHubFolderSharingService sharingService)
    {
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(worldHubServerService);
        ArgumentNullException.ThrowIfNull(sharingService);

        _googleDriveClient = googleDriveClient;
        _worldHubServerService = worldHubServerService;
        _sharingService = sharingService;
    }

    private bool _isHost = true;

    public void SetHostStatus(bool isHost)
    {
        _isHost = isHost;
        UpdateFolderUi();
    }

    public void SetServer(WorldHubServer? server)
    {
        _currentServer = server;
        UpdateFolderUi();
    }

    public void UpdateFolderUi()
    {
        if (_currentServer is null)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;

        var folderId = _currentServer.GoogleDriveFolderId;

        if (string.IsNullOrWhiteSpace(folderId))
        {
            FolderNotCreatedPanel.Visibility = Visibility.Visible;
            FolderCreatedPanel.Visibility = Visibility.Collapsed;

            if (_isHost)
            {
                FolderNotCreatedMessageText.Text = "Папка ещё не создана. Создайте её, чтобы синхронизировать миры с участниками. Доступ будет выдан всем автоматически.";
                CreateFolderButton.Visibility = Visibility.Visible;
            }
            else
            {
                FolderNotCreatedMessageText.Text = "Общая папка ещё не создана хостом сервера. Когда создатель сервера создаст её, она появится здесь автоматически.";
                CreateFolderButton.Visibility = Visibility.Collapsed;
            }
            return;
        }

        FolderNotCreatedPanel.Visibility = Visibility.Collapsed;
        FolderCreatedPanel.Visibility = Visibility.Visible;

        FolderIdText.Text = folderId;
        OpenInGoogleDriveButton.IsEnabled = !string.IsNullOrWhiteSpace(folderId);

        if (FolderAccessTimeText.Visibility != Visibility.Visible)
        {
            FolderAccessStatusText.Text = "Доступ не проверен";
            FolderAccessStatusText.Foreground = UnknownBrush;
            FolderAccessTimeText.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Вызывается из ConnectionView после проверки участников,
    /// чтобы обновить доступ для тех, кто ещё не был приглашён.
    /// </summary>
    public async Task ShareWithCurrentParticipantsAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentServer is null || _sharingService is null)
        {
            return;
        }

        if (_worldHubServerService is not null)
        {
            var freshServer = await _worldHubServerService.GetByIdAsync(
                _currentServer.Id,
                cancellationToken);
            if (freshServer is not null)
            {
                _currentServer = freshServer;
            }
        }

        if (string.IsNullOrWhiteSpace(_currentServer.GoogleDriveFolderId))
        {
            return;
        }

        await _sharingService.ShareWithAllParticipantsAsync(
            _currentServer,
            cancellationToken);
    }

    private async void CreateFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_currentServer is null ||
            _googleDriveClient is null ||
            _worldHubServerService is null ||
            _sharingService is null)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        CreateFolderButton.IsEnabled = false;

        try
        {
            var folderName = $"WorldHub — {_currentServer.Name}";

            AppLog.Log($"[FOLDER] Creating folder '{folderName}' ...");

            var folderId = await _googleDriveClient.CreateFolderAsync(folderName);

            if (string.IsNullOrWhiteSpace(folderId))
            {
                MessageBox.Show(
                    owner,
                    "Google Drive не вернул ID созданной папки.",
                    "WorldHub",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            AppLog.Success($"[FOLDER] Folder created: {folderId}");

            _currentServer.GoogleDriveFolderId = folderId;

            try
            {
                _currentServer.GoogleDriveOwnerEmail =
                    await _googleDriveClient.GetAccountEmailAsync();
            }
            catch
            {
            }

            await _worldHubServerService.UpdateAsync(_currentServer);
            ServerUpdated?.Invoke(this, _currentServer);
            UpdateFolderUi();

            AppLog.Log("[FOLDER] Server updated, starting auto-share ...");

            await _sharingService.ShareWithAllParticipantsAsync(_currentServer);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                owner,
                $"Не удалось создать папку.\n\n{exception.Message}",
                "WorldHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            CreateFolderButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Проверяет доступ текущего аккаунта к папке текущего WorldHub-сервера
    /// и обновляет UI. Вызывается из ConnectionView после проверки участников.
    /// </summary>
    public async Task RefreshFolderAccessAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentServer is null ||
            string.IsNullOrWhiteSpace(_currentServer.GoogleDriveFolderId) ||
            _googleDriveClient is null)
        {
            return;
        }

        try
        {
            AppLog.Log(
                $"[FOLDER] Checking access to {_currentServer.GoogleDriveFolderId} ...");

            var status = await _googleDriveClient.CheckFolderAccessAsync(
                _currentServer.GoogleDriveFolderId!,
                cancellationToken);

            AppLog.Log($"[FOLDER] Access status: {status}");

            FolderAccessStatusText.Text =
                GoogleDriveStatusUiHelper.GetFolderStatusText(status);
            FolderAccessStatusText.Foreground =
                GoogleDriveStatusUiHelper.GetFolderStatusBrush(status);

            FolderAccessTimeText.Text =
                $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
            FolderAccessTimeText.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            FolderAccessStatusText.Text =
                $"Ошибка проверки: {exception.Message}";
            FolderAccessStatusText.Foreground = OfflineBrush;

            FolderAccessTimeText.Text =
                $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
            FolderAccessTimeText.Visibility = Visibility.Visible;
        }
    }

    private void OpenInGoogleDriveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_currentServer is null ||
            string.IsNullOrWhiteSpace(_currentServer.GoogleDriveFolderId))
        {
            return;
        }

        try
        {
            var url = $"https://drive.google.com/drive/folders/{_currentServer.GoogleDriveFolderId}";
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
}