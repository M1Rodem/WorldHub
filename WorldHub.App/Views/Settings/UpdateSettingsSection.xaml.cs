using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services.Update;
using WorldHub.App.Views.Dialogs;

namespace WorldHub.App.Views.Settings;

public partial class UpdateSettingsSection : UserControl
{
    private AppVersionService? _appVersionService;
    private UpdaterProcessService? _updaterProcessService;
    private bool _updateAvailable;
    private string? _latestVersion;

    public UpdateSettingsSection()
    {
        InitializeComponent();
    }

    public void Initialize(
        AppVersionService appVersionService,
        UpdaterProcessService updaterProcessService)
    {
        ArgumentNullException.ThrowIfNull(appVersionService);
        ArgumentNullException.ThrowIfNull(updaterProcessService);

        _appVersionService = appVersionService;
        _updaterProcessService = updaterProcessService;

        AppVersionText.Text = _appVersionService.GetVersion();
    }

    private async void CheckUpdateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appVersionService is null ||
            _updaterProcessService is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Сервис обновлений ещё не инициализирован.");
            return;
        }

        try
        {
            CheckUpdateButton.IsEnabled = false;
            UpdateButton.IsEnabled = false;

            UpdateStatusText.Text = "● Проверка обновлений...";
            LatestVersionText.Visibility = Visibility.Collapsed;
            LastUpdateCheckText.Visibility = Visibility.Collapsed;

            var currentVersion = _appVersionService.GetVersion();

            using var process = _updaterProcessService.StartCheck(currentVersion);

            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = await standardOutputTask;
            var error = await standardErrorTask;

            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "Updater вернул пустой ответ."
                        : error);
            }

            var result = JsonSerializer.Deserialize<UpdateCheckOutput>(output);

            if (result is null)
            {
                throw new InvalidOperationException("Updater вернул некорректный ответ.");
            }

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    result.Error ?? "Не удалось проверить обновления.");
            }

            _updateAvailable = result.UpdateAvailable;
            _latestVersion = result.LatestVersion;

            if (result.UpdateAvailable)
            {
                UpdateStatusText.Text = $"● Доступна новая версия {result.LatestVersion}";
                LatestVersionText.Text = $"Новая версия: {result.LatestVersion}";
                LatestVersionText.Visibility = Visibility.Visible;
                UpdateButton.IsEnabled = true;
            }
            else
            {
                UpdateStatusText.Text = "● Установлена последняя версия";
                UpdateButton.IsEnabled = false;
            }

            LastUpdateCheckText.Text = $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";
            LastUpdateCheckText.Visibility = Visibility.Visible;
        }
        catch (Exception exception)
        {
            _updateAvailable = false;
            _latestVersion = null;
            UpdateButton.IsEnabled = false;

            UpdateStatusText.Text = "● Не удалось проверить обновления";
            LastUpdateCheckText.Text = $"Ошибка: {exception.Message}";
            LastUpdateCheckText.Visibility = Visibility.Visible;
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private async void UpdateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appVersionService is null || _updaterProcessService is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Сервис обновлений ещё не инициализирован.");
            return;
        }

        if (!_updateAvailable)
        {
            DialogWindow.ShowInformation(
                Window.GetWindow(this)!,
                "WorldHub",
                "Доступных обновлений нет.");
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        if (!DialogWindow.ShowConfirmation(
                owner,
                "Обновление WorldHub",
                $"Установить новую версию {_latestVersion}?\n\n" +
                "WorldHub будет закрыт и запущен снова после обновления."))
        {
            return;
        }

        try
        {
            UpdateButton.IsEnabled = false;
            CheckUpdateButton.IsEnabled = false;

            var currentVersion = _appVersionService.GetVersion();
            _updaterProcessService.StartUpdate(currentVersion);

            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            UpdateButton.IsEnabled = true;
            CheckUpdateButton.IsEnabled = true;

            DialogWindow.ShowError(
                owner,
                "Ошибка обновления",
                exception.Message);
        }
    }
}
