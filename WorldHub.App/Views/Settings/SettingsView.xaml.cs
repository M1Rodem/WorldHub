using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services.Settings;
using WorldHub.App.Services.Update;
using WorldHub.App.ViewModels.Worlds;
using WorldHub.Sync.Services;
using WorldHub.App.Views.Dialogs;

namespace WorldHub.App.Views.Settings;

public partial class SettingsView : UserControl
{
    private WorldDeletionService? _worldDeletionService;
    private List<WorldViewModel>? _worlds;
    private Func<Task>? _dataChangedHandler;
    private AppVersionService? _appVersionService;
    private UpdaterProcessService? _updaterProcessService;
    private AppSettingsService? _appSettingsService;
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
    AppVersionService appVersionService,
    AppSettingsService appSettingsService)
    {
        ArgumentNullException.ThrowIfNull(worldDeletionService);
        ArgumentNullException.ThrowIfNull(worlds);
        ArgumentNullException.ThrowIfNull(appVersionService);
        ArgumentNullException.ThrowIfNull(appSettingsService);

        _worldDeletionService = worldDeletionService;
        _worlds = worlds;
        _dataChangedHandler = dataChangedHandler;
        _appVersionService = appVersionService;
        _appSettingsService = appSettingsService;

        _updaterProcessService = new UpdaterProcessService();

        AppVersionText.Text = _appVersionService.GetVersion();
        DataPathText.Text = _appSettingsService.GetDataPath();

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
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Настройки ещё не инициализированы.");

            return;
        }

        var selectedWorlds = _worlds
            .Where(world => world.IsSelected)
            .ToList();

        if (selectedWorlds.Count == 0)
        {
            DialogWindow.ShowInformation(
                Window.GetWindow(this)!,
                "Миры не выбраны",
                "Выберите хотя бы один мир для удаления.");

            return;
        }

        var worldNames = string.Join(
            "\n",
            selectedWorlds.Select(world => $"• {world.Name}"));

        if (!DialogWindow.ShowConfirmation(
            Window.GetWindow(this)!,
            "Удаление миров из WorldHub",
            "Удалить выбранные миры из WorldHub?\n\n" +
            $"{worldNames}\n\n" +
            "Будет удалено:\n" +
            "• регистрация этих миров в WorldHub;\n" +
            "• все локальные snapshots;\n" +
            "• история snapshots;\n" +
            "• локальные связи WorldHub с этими мирами.\n\n" +
            "Физические миры Minecraft НЕ будут удалены.\n" +
            "Папки миров в Minecraft\\saves останутся без изменений.\n\n" +
            "Это действие нельзя отменить."))
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

            DialogWindow.ShowInformation(
                Window.GetWindow(this)!,
                "Удаление завершено",
                selectedWorlds.Count == 1
                    ? $"Мир «{selectedWorlds[0].Name}» удалён из WorldHub.\n\n" +
                      "Физический мир Minecraft сохранён."
                    : $"Удалено миров: {selectedWorlds.Count}.\n\n" +
                      "Физические миры Minecraft сохранены.");
        }
        catch (Exception exception)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка удаления",
                exception.Message);
        }
    }

    private async void ChangeDataPathButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (_appSettingsService is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Сервис настроек ещё не инициализирован.");

            return;
        }

        var currentPath =
            Path.GetFullPath(
                _appSettingsService.GetDataPath());

        var legacyRootPath =
            Directory.GetParent(currentPath)?.FullName
            ?? throw new InvalidOperationException(
                "Не удалось определить корневую папку текущего хранилища.");

        using var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Multiselect = false,
            Title = "Выберите папку для данных WorldHub"
        };

        if (Directory.Exists(currentPath))
        {
            dialog.InitialDirectory = currentPath;
        }

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
        {
            return;
        }

        var newPath =
            Path.GetFullPath(dialog.FileName);

        var newDataPath =
            Path.Combine(
                newPath,
                "data");

        if (string.Equals(
                currentPath,
                newPath,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                legacyRootPath,
                newPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (newPath.StartsWith(
                legacyRootPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            legacyRootPath.StartsWith(
                newPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            DialogWindow.ShowWarning(
                Window.GetWindow(this)!,
                "Недопустимая папка",
                "Новая папка не может находиться внутри текущего хранилища WorldHub.");

            return;
        }

        if (Directory.Exists(newPath) &&
            Directory.EnumerateFileSystemEntries(newPath).Any())
        {
            DialogWindow.ShowWarning(
                Window.GetWindow(this)!,
                "Папка не пуста",
                "Выбранная папка не пуста.\n\n" +
                "Для безопасности выберите пустую папку.");

            return;
        }

        var owner =
            Window.GetWindow(this);

        if (owner is null)
        {
            return;
        }

        if (!DialogWindow.ShowConfirmation(
                owner,
                currentPath,
                newPath))
        {
            return;
        }

        try
        {
            ChangeDataPathButton.IsEnabled = false;

            var legacySettingsPath =
                Path.Combine(
                    legacyRootPath,
                    "settings.json");

            var newSettingsPath =
                Path.Combine(
                    newPath,
                    "settings.json");

            await Task.Run(() =>
            {
                MoveDirectoryContents(
                    currentPath,
                    newDataPath);

                if (File.Exists(legacySettingsPath))
                {
                    File.Move(
                        legacySettingsPath,
                        newSettingsPath,
                        overwrite: true);
                }
            });

            _appSettingsService.SaveDataPath(
                newPath);

            if (Directory.Exists(legacyRootPath))
            {
                Directory.Delete(
                    legacyRootPath,
                    recursive: true);
            }

            DataPathText.Text =
                newPath;

            DialogWindow.ShowCompleted(
                owner,
                newPath);
        }
        catch (Exception exception)
        {
            DialogWindow.ShowError(
                owner,
                "Ошибка переноса",
                $"Не удалось перенести данные.\n\n{exception.Message}");
        }
        finally
        {
            ChangeDataPathButton.IsEnabled = true;
        }
    }

    private async void ResetDataPathButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appSettingsService is null)
        {
            return;
        }

        var owner =
            Window.GetWindow(this);

        if (owner is null)
        {
            return;
        }

        var currentPath =
            Path.GetFullPath(
                _appSettingsService.GetDataPath());

        var defaultDataPath =
            Path.GetFullPath(
                _appSettingsService.GetDefaultDataPath());

        var defaultRootPath =
            Directory.GetParent(defaultDataPath)?.FullName
            ?? throw new InvalidOperationException(
                "Не удалось определить стандартную папку WorldHub.");

        if (string.Equals(
                currentPath,
                defaultDataPath,
                StringComparison.OrdinalIgnoreCase))
        {
            DialogWindow.ShowInformation(
                owner,
                "Сброс пути",
                "Путь данных уже установлен по умолчанию.");

            return;
        }

        if (!DialogWindow.ShowConfirmation(
                owner,
                "Сброс пути данных",
                "Все локальные данные WorldHub будут перенесены " +
                "в стандартную папку.\n\n" +
                $"Новое расположение:\n{defaultRootPath}\n\n" +
                "Продолжить?"))
        {
            return;
        }

        try
        {
            ChangeDataPathButton.IsEnabled = false;
            ResetDataPathButton.IsEnabled = false;

            var legacyRootPath =
                Directory.GetParent(currentPath)?.FullName
                ?? throw new InvalidOperationException(
                    "Не удалось определить корневую папку текущего хранилища.");

            var legacySettingsPath =
                Path.Combine(
                    legacyRootPath,
                    "settings.json");

            var defaultSettingsPath =
                Path.Combine(
                    defaultRootPath,
                    "settings.json");

            Directory.CreateDirectory(
                defaultRootPath);

            await Task.Run(() =>
            {
                MoveDirectoryContents(
                    currentPath,
                    defaultDataPath);

                if (File.Exists(legacySettingsPath))
                {
                    File.Move(
                        legacySettingsPath,
                        defaultSettingsPath,
                        overwrite: true);
                }
            });

            _appSettingsService.SaveDataPath(
                defaultRootPath);

            if (Directory.Exists(legacyRootPath))
            {
                Directory.Delete(
                    legacyRootPath,
                    recursive: true);
            }

            DataPathText.Text =
                defaultRootPath;

            DialogWindow.ShowCompleted(
                owner,
                defaultRootPath);
        }
        catch (Exception exception)
        {
            DialogWindow.ShowError(
                owner,
                "Ошибка сброса",
                $"Не удалось сбросить путь данных.\n\n" +
                exception.Message);
        }
        finally
        {
            ChangeDataPathButton.IsEnabled = true;
            ResetDataPathButton.IsEnabled = true;
        }
    }

    private static void MoveDirectoryContents(
    string sourceDirectory,
    string destinationDirectory)
    {
        Directory.CreateDirectory(
            destinationDirectory);

        foreach (var directory in
                 Directory.EnumerateDirectories(
                     sourceDirectory))
        {
            var directoryName =
                Path.GetFileName(directory);

            var destination =
                Path.Combine(
                    destinationDirectory,
                    directoryName);

            MoveDirectoryContents(
                directory,
                destination);
        }

        foreach (var file in
                 Directory.EnumerateFiles(
                     sourceDirectory))
        {
            var fileName =
                Path.GetFileName(file);

            var destination =
                Path.Combine(
                    destinationDirectory,
                    fileName);

            File.Move(
                file,
                destination);
        }
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

    private async void UpdateButton_Click(
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


        if (!_updateAvailable)
        {
            DialogWindow.ShowInformation(
                Window.GetWindow(this)!,
                "WorldHub",
                "Доступных обновлений нет.");

            return;
        }


        try
        {
            UpdateButton.IsEnabled = false;
            CheckUpdateButton.IsEnabled = false;


            var owner =
                Window.GetWindow(this);


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


            var currentVersion =
                _appVersionService.GetVersion();


            _updaterProcessService.StartUpdate(
                currentVersion);


            Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            UpdateButton.IsEnabled = true;
            CheckUpdateButton.IsEnabled = true;


            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка обновления",
                exception.Message);
        }
    }
}