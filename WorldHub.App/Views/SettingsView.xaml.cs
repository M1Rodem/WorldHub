using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services;
using WorldHub.App.ViewModels;
using WorldHub.Sync.Services;
using WorldHub.Updater.Models;

namespace WorldHub.App.Views;

public partial class SettingsView : UserControl
{
    private WorldDeletionService? _worldDeletionService;
    private List<WorldViewModel>? _worlds;
    private Func<Task>? _dataChangedHandler;
    private AppVersionService? _appVersionService;
    private UpdaterProcessService? _updaterProcessService;
    private bool _updateAvailable;
    private string? _latestVersion;
    private string? _downloadUrl;
    public SettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(
        WorldDeletionService worldDeletionService,
        List<WorldViewModel> worlds,
        Func<Task>? dataChangedHandler,
        AppVersionService appVersionService)
    {
        ArgumentNullException.ThrowIfNull(worldDeletionService);
        ArgumentNullException.ThrowIfNull(worlds);
        ArgumentNullException.ThrowIfNull(appVersionService);

        _worldDeletionService = worldDeletionService;
        _worlds = worlds;
        _dataChangedHandler = dataChangedHandler;
        _appVersionService = appVersionService;
        _updaterProcessService = new UpdaterProcessService();

        AppVersionText.Text = _appVersionService.GetVersion();

        UpdateStatusText.Text =
            "● Проверка ещё не выполнялась";

        UpdateButton.IsEnabled = false;

        WorldsList.ItemsSource = _worlds;
    }

    public void RefreshWorlds(List<WorldViewModel> worlds)
    {
        ArgumentNullException.ThrowIfNull(worlds);

        _worlds = worlds;

        WorldsList.ItemsSource = null;
        WorldsList.ItemsSource = _worlds;
    }

    private async void DeleteWorldButton_Click(object sender, RoutedEventArgs e)
    {
        if (_worldDeletionService is null || _worlds is null)
        {
            MessageBox.Show(
                "Настройки ещё не инициализированы.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        var selectedWorlds = _worlds
            .Where(world => world.IsSelected)
            .ToList();

        if (selectedWorlds.Count == 0)
        {
            MessageBox.Show(
                "Выберите хотя бы один мир для удаления.",
                "Миры не выбраны",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        var worldNames = string.Join(
            "\n",
            selectedWorlds.Select(world => $"• {world.Name}"));

        var result = MessageBox.Show(
            "Удалить выбранные миры из WorldHub?\n\n" +
            $"{worldNames}\n\n" +
            "Будет удалено:\n" +
            "• регистрация этих миров в WorldHub;\n" +
            "• все локальные snapshots;\n" +
            "• история snapshots;\n" +
            "• локальные связи WorldHub с этими мирами.\n\n" +
            "Физические миры Minecraft НЕ будут удалены.\n" +
            "Папки миров в Minecraft\\saves останутся без изменений.\n\n" +
            "Это действие нельзя отменить.",
            "Удаление миров из WorldHub",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            foreach (var world in selectedWorlds)
            {
                await _worldDeletionService.DeleteAsync(world.Id);
            }

            foreach (var world in selectedWorlds)
            {
                _worlds.Remove(world);
            }

            WorldsList.ItemsSource = null;
            WorldsList.ItemsSource = _worlds;

            if (_dataChangedHandler is not null)
            {
                await _dataChangedHandler();
            }

            MessageBox.Show(
                selectedWorlds.Count == 1
                    ? $"Мир «{selectedWorlds[0].Name}» удалён из WorldHub.\n\n" +
                      "Физический мир Minecraft сохранён."
                    : $"Удалено миров: {selectedWorlds.Count}.\n\n" +
                      "Физические миры Minecraft сохранены.",
                "Удаление завершено",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Ошибка удаления",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
    private async void CheckUpdateButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (_appVersionService is null ||
            _updaterProcessService is null)
        {
            MessageBox.Show(
                "Сервис обновлений ещё не инициализирован.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        try
        {
            CheckUpdateButton.IsEnabled = false;
            UpdateButton.IsEnabled = false;

            UpdateStatusText.Text =
                "● Проверка обновлений...";

            LatestVersionText.Visibility =
                Visibility.Collapsed;

            LastUpdateCheckText.Visibility =
                Visibility.Collapsed;

            var currentVersion =
                _appVersionService.GetVersion();

            using var process =
                _updaterProcessService.StartCheck(
                    currentVersion);

            var standardOutputTask =
                process.StandardOutput.ReadToEndAsync();

            var standardErrorTask =
                process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output =
                await standardOutputTask;

            var error =
                await standardErrorTask;

            if (string.IsNullOrWhiteSpace(output))
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "Updater returned an empty response."
                        : error);
            }

            var result =
                JsonSerializer.Deserialize<UpdateCheckOutput>(
                    output);

            if (result is null)
            {
                throw new InvalidOperationException(
                    "Updater returned an invalid response.");
            }

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    result.Error ??
                    "Failed to check for updates.");
            }

            _updateAvailable =
                result.UpdateAvailable;

            _latestVersion =
                result.LatestVersion;

            _downloadUrl =
                result.DownloadUrl;

            if (result.UpdateAvailable)
            {
                UpdateStatusText.Text =
                    $"● Доступна новая версия {result.LatestVersion}";

                LatestVersionText.Text =
                    $"Новая версия: {result.LatestVersion}";

                LatestVersionText.Visibility =
                    Visibility.Visible;

                UpdateButton.IsEnabled = true;
            }
            else
            {
                UpdateStatusText.Text =
                    "● Установлена последняя версия";

                UpdateButton.IsEnabled = false;
            }

            LastUpdateCheckText.Text =
                $"Последняя проверка: {DateTime.Now:dd.MM.yyyy HH:mm}";

            LastUpdateCheckText.Visibility =
                Visibility.Visible;
        }
        catch (Exception exception)
        {
            _updateAvailable = false;
            _latestVersion = null;
            _downloadUrl = null;

            UpdateButton.IsEnabled = false;

            UpdateStatusText.Text =
                "● Не удалось проверить обновления";

            LastUpdateCheckText.Text =
                $"Ошибка: {exception.Message}";

            LastUpdateCheckText.Visibility =
                Visibility.Visible;
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void UpdateButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "Установка обновления будет подключена следующим этапом.",
            "WorldHub",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}