using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WorldHub.App.Services.Network;
using WorldHub.App.Services.Settings;
using WorldHub.App.Views.Dialogs;
using WorldHub.Infrastructure.Google;

namespace WorldHub.App.Views.Settings;

public partial class ProfileSettingsSection : UserControl
{
    private AppSettingsService? _appSettingsService;
    private GoogleDriveClient? _googleDriveClient;
    private GoogleDriveStatusCache? _googleDriveStatusCache;
    private GoogleAuthService? _googleAuthService;
    private DispatcherTimer? _userNameSaveTimer;

    public ProfileSettingsSection()
    {
        InitializeComponent();
        Unloaded += ProfileSettingsSection_Unloaded;
    }

    public void Initialize(
        AppSettingsService appSettingsService,
        GoogleDriveClient googleDriveClient,
        GoogleDriveStatusCache googleDriveStatusCache)
    {
        ArgumentNullException.ThrowIfNull(appSettingsService);
        ArgumentNullException.ThrowIfNull(googleDriveClient);
        ArgumentNullException.ThrowIfNull(googleDriveStatusCache);

        _appSettingsService = appSettingsService;
        _googleDriveClient = googleDriveClient;
        _googleDriveStatusCache = googleDriveStatusCache;
        _googleAuthService = googleDriveClient.AuthService;

        UserNameTextBox.Text = _appSettingsService.GetUserName();

        UserNameTextBox.LostFocus -= UserNameTextBox_LostFocus;
        UserNameTextBox.LostFocus += UserNameTextBox_LostFocus;

        _ = RestoreGoogleAccountAsync();
    }

    private void UserNameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        FlushUserNameSave();
    }

    private void ProfileSettingsSection_Unloaded(object sender, RoutedEventArgs e)
    {
        FlushUserNameSave();
    }

    public void FlushUserNameSave()
    {
        if (_userNameSaveTimer?.IsEnabled != true)
        {
            return;
        }

        _userNameSaveTimer.Stop();

        var value = UserNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(value) || _appSettingsService is null)
        {
            return;
        }

        try
        {
            _appSettingsService.SaveUserName(value);
        }
        catch
        {
        }
    }

    private void UserNameTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_appSettingsService is null)
        {
            return;
        }

        if (_userNameSaveTimer is null)
        {
            _userNameSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };

            _userNameSaveTimer.Tick += (_, _) =>
            {
                _userNameSaveTimer!.Stop();

                var value = UserNameTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }

                try
                {
                    _appSettingsService.SaveUserName(value);
                }
                catch
                {
                }
            };
        }

        _userNameSaveTimer.Stop();
        _userNameSaveTimer.Start();
    }

    public async Task RestoreGoogleAccountAsync()
    {
        if (_googleAuthService is null ||
            _googleDriveClient is null ||
            _googleDriveStatusCache is null)
        {
            return;
        }

        if (!_googleAuthService.HasSavedToken())
        {
            return;
        }

        try
        {
            var email = await _googleDriveClient.GetAccountEmailAsync();

            GoogleAccountText.Text = email;
            GoogleAccountIcon.Source = new Uri(
                "/Assets/Icons/google-on.svg",
                UriKind.Relative);
            GoogleConnectButton.Content = "Отключить";

            try
            {
                var result = await _googleDriveClient.CheckDriveAccessAsync();
                _googleDriveStatusCache.Update(result);

                DriveCheckStatusText.Text =
                    GoogleDriveStatusUiHelper.GetStatusMessage(result);
                DriveCheckStatusText.Foreground =
                    GoogleDriveStatusUiHelper.GetStatusBrush(result);
                DriveCheckTimeText.Text =
                    $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
                DriveCheckTimeText.Visibility = Visibility.Visible;
            }
            catch
            {
            }
        }
        catch
        {
        }
    }

    private async void GoogleConnectButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_googleDriveClient is null || _googleAuthService is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Google-сервис ещё не инициализирован.");
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        if (_googleAuthService.HasSavedToken())
        {
            try
            {
                GoogleConnectButton.IsEnabled = false;
                GoogleConnectButton.Content = "Отключение...";

                await _googleAuthService.SignOutAsync();

                GoogleAccountText.Text = "Google аккаунт не подключен";
                GoogleAccountIcon.Source = new Uri(
                    "/Assets/Icons/google-off.svg",
                    UriKind.Relative);
                GoogleConnectButton.Content = "Подключить";

                if (_googleDriveStatusCache is not null)
                {
                    _googleDriveStatusCache.Update(
                        GoogleDriveCheckResult.NotAuthorized);

                    DriveCheckStatusText.Text =
                        GoogleDriveStatusUiHelper.GetStatusMessage(
                            GoogleDriveCheckResult.NotAuthorized);
                    DriveCheckStatusText.Foreground =
                        GoogleDriveStatusUiHelper.GetStatusBrush(
                            GoogleDriveCheckResult.NotAuthorized);
                    DriveCheckTimeText.Text =
                        $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
                    DriveCheckTimeText.Visibility = Visibility.Visible;
                }
            }
            catch (Exception exception)
            {
                DialogWindow.ShowError(
                    owner,
                    "Ошибка Google",
                    $"Не удалось отключить Google аккаунт.\n\n{exception.Message}");
            }
            finally
            {
                GoogleConnectButton.IsEnabled = true;
            }

            return;
        }

        if (!_googleAuthService.HasCredentialsFile)
        {
            DialogWindow.ShowError(
                owner,
                "Google",
                "Файл credentials.json не найден.\n\n" +
                "Поместите его в папку:\n" +
                "WorldHub.Infrastructure\\Google\\credentials.json");
            return;
        }

        try
        {
            GoogleConnectButton.IsEnabled = false;
            GoogleConnectButton.Content = "Подключение...";

            var email = await _googleDriveClient.GetAccountEmailAsync();

            GoogleAccountText.Text = email;
            GoogleAccountIcon.Source = new Uri(
                "/Assets/Icons/google-on.svg",
                UriKind.Relative);
            GoogleConnectButton.Content = "Отключить";

            if (_googleDriveStatusCache is not null)
            {
                try
                {
                    var result = await _googleDriveClient.CheckDriveAccessAsync();
                    _googleDriveStatusCache.Update(result);

                    DriveCheckStatusText.Text =
                        GoogleDriveStatusUiHelper.GetStatusMessage(result);
                    DriveCheckStatusText.Foreground =
                        GoogleDriveStatusUiHelper.GetStatusBrush(result);
                    DriveCheckTimeText.Text =
                        $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
                    DriveCheckTimeText.Visibility = Visibility.Visible;
                }
                catch
                {
                }
            }

            DialogWindow.ShowInformation(
                owner,
                "Google",
                $"Google аккаунт подключён:\n\n{email}");
        }
        catch (Exception exception)
        {
            DialogWindow.ShowError(
                owner,
                "Ошибка Google",
                $"Не удалось подключить Google аккаунт.\n\n{exception.Message}");
        }
        finally
        {
            GoogleConnectButton.IsEnabled = true;
        }
    }

    private async void DriveCheckButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_googleDriveClient is null || _googleDriveStatusCache is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Google-сервис ещё не инициализирован.");
            return;
        }

        DriveCheckButton.IsEnabled = false;
        DriveCheckButton.Content = "Проверка...";

        DriveCheckStatusText.Text = "Проверка...";
        DriveCheckStatusText.Foreground = new SolidColorBrush(
            Color.FromRgb(0xA9, 0xB1, 0xBF));
        DriveCheckTimeText.Visibility = Visibility.Collapsed;

        try
        {
            var result = await _googleDriveClient.CheckDriveAccessAsync();
            _googleDriveStatusCache.Update(result);

            DriveCheckStatusText.Text =
                GoogleDriveStatusUiHelper.GetStatusMessage(result);
            DriveCheckStatusText.Foreground =
                GoogleDriveStatusUiHelper.GetStatusBrush(result);
            DriveCheckTimeText.Text =
                $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
            DriveCheckTimeText.Visibility = Visibility.Visible;
        }
        catch (Exception exception)
        {
            DriveCheckStatusText.Text = $"Ошибка проверки: {exception.Message}";
            DriveCheckStatusText.Foreground = new SolidColorBrush(
                Color.FromRgb(0xE5, 0x6B, 0x6F));
        }
        finally
        {
            DriveCheckButton.IsEnabled = true;
            DriveCheckButton.Content = "Проверить подключение";
        }
    }
}
