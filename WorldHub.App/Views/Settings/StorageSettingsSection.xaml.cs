using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services.Settings;
using WorldHub.App.Views.Dialogs;

namespace WorldHub.App.Views.Settings;

public partial class StorageSettingsSection : UserControl
{
    private AppSettingsService? _appSettingsService;
    private DataMigrationService? _dataMigrationService;

    public StorageSettingsSection()
    {
        InitializeComponent();
    }

    public void Initialize(
        AppSettingsService appSettingsService,
        DataMigrationService dataMigrationService)
    {
        ArgumentNullException.ThrowIfNull(appSettingsService);
        ArgumentNullException.ThrowIfNull(dataMigrationService);

        _appSettingsService = appSettingsService;
        _dataMigrationService = dataMigrationService;

        DataPathText.Text = _appSettingsService.GetDataPath();
    }

    private async void ChangeDataPathButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appSettingsService is null || _dataMigrationService is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Сервис настроек ещё не инициализирован.");
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        var currentDataPath = Path.GetFullPath(
            _appSettingsService.GetDataPath());

        var currentRootPath =
            Directory.GetParent(currentDataPath)?.FullName
            ?? throw new InvalidOperationException(
                "Не удалось определить корневую папку WorldHub.");

        using var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Multiselect = false,
            Title = "Выберите родительскую папку для WorldHub"
        };

        if (Directory.Exists(currentRootPath))
        {
            dialog.InitialDirectory =
                Directory.GetParent(currentRootPath)?.FullName
                ?? currentRootPath;
        }

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
        {
            return;
        }

        var parentPath = Path.GetFullPath(dialog.FileName);

        var newRootPath = Path.Combine(
            parentPath,
            "WorldHub");

        var newDataPath = Path.Combine(
            newRootPath,
            "data");

        if (string.Equals(
                currentRootPath,
                newRootPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (newRootPath.StartsWith(
                currentRootPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) ||
            currentRootPath.StartsWith(
                newRootPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            DialogWindow.ShowWarning(
                owner,
                "Недопустимая папка",
                "Новая папка не может находиться внутри текущего хранилища WorldHub.");
            return;
        }

        if (Directory.Exists(newRootPath) &&
            Directory.EnumerateFileSystemEntries(newRootPath).Any())
        {
            DialogWindow.ShowWarning(
                owner,
                "Папка не пуста",
                $"Папка {newRootPath} уже существует и не пуста.\n\n" +
                "Выберите другую родительскую папку.");
            return;
        }

        if (!DialogWindow.ShowConfirmation(
                owner,
                "Изменение папки данных",
                $"Перенести данные WorldHub?\n\n" +
                $"Текущее расположение:\n{currentDataPath}\n\n" +
                $"Новое расположение:\n{newDataPath}\n\n" +
                "Продолжить?"))
        {
            return;
        }

        try
        {
            ChangeDataPathButton.IsEnabled = false;
            ResetDataPathButton.IsEnabled = false;

            await Task.Run(() =>
            {
                _dataMigrationService.MigrateData(
                    currentDataPath,
                    newDataPath,
                    currentRootPath,
                    newRootPath);
            });

            DataPathText.Text = _appSettingsService.GetDataPath();

            DialogWindow.ShowRestartRequired(
                owner,
                _appSettingsService.GetDataPath());
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
            ResetDataPathButton.IsEnabled = true;
        }
    }

    private async void ResetDataPathButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_appSettingsService is null || _dataMigrationService is null)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        if (owner is null)
        {
            return;
        }

        var currentDataPath = Path.GetFullPath(
            _appSettingsService.GetDataPath());

        var defaultDataPath = Path.GetFullPath(
            _appSettingsService.GetDefaultDataPath());

        var defaultRootPath =
            Directory.GetParent(defaultDataPath)?.FullName
            ?? throw new InvalidOperationException(
                "Не удалось определить стандартную папку WorldHub.");

        if (string.Equals(
                currentDataPath,
                defaultDataPath,
                StringComparison.OrdinalIgnoreCase))
        {
            DialogWindow.ShowInformation(
                owner,
                "Сброс пути",
                "Путь данных уже установлен по умолчанию.");
            return;
        }

        if (Directory.Exists(defaultRootPath) &&
            Directory.EnumerateFileSystemEntries(defaultRootPath).Any())
        {
            DialogWindow.ShowWarning(
                owner,
                "Стандартная папка не пуста",
                "Невозможно выполнить сброс, пока стандартная папка WorldHub не пуста.");
            return;
        }

        if (!DialogWindow.ShowConfirmation(
                owner,
                "Сброс пути данных",
                $"Перенести данные WorldHub в стандартное расположение?\n\n" +
                $"{defaultDataPath}\n\n" +
                "Продолжить?"))
        {
            return;
        }

        try
        {
            ChangeDataPathButton.IsEnabled = false;
            ResetDataPathButton.IsEnabled = false;

            var currentRootPath =
                Directory.GetParent(currentDataPath)?.FullName
                ?? throw new InvalidOperationException(
                    "Не удалось определить корневую папку WorldHub.");

            await Task.Run(() =>
            {
                _dataMigrationService.MigrateData(
                    currentDataPath,
                    defaultDataPath,
                    currentRootPath,
                    defaultRootPath);
            });

            DataPathText.Text = _appSettingsService.GetDataPath();

            DialogWindow.ShowCompleted(
                owner,
                _appSettingsService.GetDataPath());
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
}
