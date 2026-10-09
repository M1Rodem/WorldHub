using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WorldHub.App.Services.Network;
using WorldHub.App.Views.Dialogs;
using WorldHub.Core.Entities;
using WorldHub.Infrastructure.Google;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views.Connection;

public partial class WorldHubFolderSection : UserControl
{
    private static readonly Brush OnlineBrush =
        new SolidColorBrush(Color.FromRgb(0x42, 0xC7, 0x83));

    private static readonly Brush OfflineBrush =
        new SolidColorBrush(Color.FromRgb(0xE5, 0x6B, 0x6F));

    private static readonly Brush WarningBrush =
        new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x5C));

    private static readonly Brush UnknownBrush =
        new SolidColorBrush(Color.FromRgb(0x8C, 0x95, 0xA3));

    private GoogleDriveClient? _googleDriveClient;
    private WorldHubServerService? _worldHubServerService;
    private WorldHubServer? _currentServer;

    public event EventHandler<WorldHubServer>? ServerUpdated;

    public WorldHubFolderSection()
    {
        InitializeComponent();
    }

    public void Initialize(
        GoogleDriveClient googleDriveClient,
        WorldHubServerService worldHubServerService)
    {
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(worldHubServerService);

        _googleDriveClient = googleDriveClient;
        _worldHubServerService = worldHubServerService;
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
            return;
        }

        FolderNotCreatedPanel.Visibility = Visibility.Collapsed;
        FolderCreatedPanel.Visibility = Visibility.Visible;

        FolderIdText.Text = folderId;

        if (FolderAccessTimeText.Visibility != Visibility.Visible)
        {
            FolderAccessStatusText.Text = "Доступ не проверен";
            FolderAccessStatusText.Foreground = UnknownBrush;
            FolderAccessTimeText.Visibility = Visibility.Collapsed;
        }

        ShareResultText.Visibility = Visibility.Collapsed;
        ShareEmailTextBox.Text = string.Empty;
    }

    private async void CreateFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_currentServer is null || _googleDriveClient is null || _worldHubServerService is null)
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

    private async void CheckFolderAccessButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_currentServer is null ||
            string.IsNullOrWhiteSpace(_currentServer.GoogleDriveFolderId) ||
            _googleDriveClient is null)
        {
            return;
        }

        CheckFolderAccessButton.IsEnabled = false;

        try
        {
            var status = await _googleDriveClient.CheckFolderAccessAsync(
                _currentServer.GoogleDriveFolderId!);

            FolderAccessStatusText.Text =
                GoogleDriveStatusUiHelper.GetFolderStatusText(status);
            FolderAccessStatusText.Foreground =
                GoogleDriveStatusUiHelper.GetFolderStatusBrush(status);

            FolderAccessTimeText.Text =
                $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
            FolderAccessTimeText.Visibility = Visibility.Visible;
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
        finally
        {
            CheckFolderAccessButton.IsEnabled = true;
        }
    }

    private async void ShareFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_currentServer is null ||
            string.IsNullOrWhiteSpace(_currentServer.GoogleDriveFolderId) ||
            _googleDriveClient is null)
        {
            return;
        }

        var email = ShareEmailTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            ShareResultText.Text = "Введите Google-email участника.";
            ShareResultText.Foreground = WarningBrush;
            ShareResultText.Visibility = Visibility.Visible;
            return;
        }

        ShareFolderButton.IsEnabled = false;
        ShareResultText.Text = "Выдача доступа...";
        ShareResultText.Foreground = UnknownBrush;
        ShareResultText.Visibility = Visibility.Visible;

        try
        {
            var result = await _googleDriveClient.ShareFolderAsync(
                _currentServer.GoogleDriveFolderId!,
                email,
                role: "writer");

            if (result.Success)
            {
                ShareResultText.Text =
                    "✓ Приглашение отправлено. Участник должен проверить доступ под своим аккаунтом.";
                ShareResultText.Foreground = OnlineBrush;
            }
            else
            {
                ShareResultText.Text = $"✗ {result.Message}";
                ShareResultText.Foreground = OfflineBrush;
            }
        }
        catch (Exception exception)
        {
            ShareResultText.Text = $"✗ {exception.Message}";
            ShareResultText.Foreground = OfflineBrush;
        }
        finally
        {
            ShareFolderButton.IsEnabled = true;
        }
    }

    public async Task<bool> InviteParticipantAsync(
        WorldHubParticipantViewModel vm,
        Window owner)
    {
        if (_currentServer is null ||
            string.IsNullOrWhiteSpace(_currentServer.GoogleDriveFolderId) ||
            _googleDriveClient is null ||
            !vm.HasGoogleEmail)
        {
            return false;
        }

        var email = vm.Participant.GoogleEmail!;

        var confirmed = DialogWindow.ShowConfirmation(
            owner,
            "Приглашение в общую папку",
            $"Пригласить «{vm.DisplayName}» ({email}) в общую папку Google Drive?\n\n" +
            "Участник сразу получит доступ редактора. " +
            "Письмо-уведомление не отправляется.");

        if (!confirmed)
        {
            return false;
        }

        try
        {
            var result = await _googleDriveClient.ShareFolderAsync(
                _currentServer.GoogleDriveFolderId!,
                email,
                role: "writer",
                sendNotificationEmail: false);

            if (result.Success)
            {
                MessageBox.Show(
                    owner,
                    $"✓ Доступ выдан: {email}",
                    "WorldHub",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return true;
            }
            else
            {
                MessageBox.Show(
                    owner,
                    $"✗ Не удалось выдать доступ.\n\n{result.Message}",
                    "WorldHub",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                owner,
                $"✗ Ошибка.\n\n{exception.Message}",
                "WorldHub",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
}
