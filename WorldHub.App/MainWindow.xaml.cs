using System.IO;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using WorldHub.App.Services;
using WorldHub.App.ViewModels;
using WorldHub.Sync.Services;

namespace WorldHub.App;

public partial class MainWindow
{
    private readonly WorldAppService _worldAppService;

    private readonly SnapshotAppService _snapshotAppService;

    private readonly List<WorldViewModel> _worlds = [];

    private readonly Guid _localPlayerId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    public MainWindow()
    {
        DebugConsole.Log("Creating MainWindow...");

        InitializeComponent();

        DebugConsole.Log("MainWindow UI initialized.");

        var worldRepository =
            WorldRepositoryFactory.Create();

        DebugConsole.Log("World repository initialized.");

        var worldService =
            new WorldService(worldRepository);

        DebugConsole.Log("World service initialized.");

        _worldAppService =
            new WorldAppService(worldService);

        var snapshotService =
            SnapshotServiceFactory.Create();

        _snapshotAppService =
            new SnapshotAppService(snapshotService);

        DebugConsole.Log(
            "Snapshot service initialized.");

        DebugConsole.Log("World application service initialized.");

        Loaded += MainWindow_Loaded;
    }

    private async void CreateSnapshotButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            DebugConsole.Error(
                "Snapshot button sender is invalid.");

            return;
        }

        if (element.DataContext is not WorldViewModel worldViewModel)
        {
            DebugConsole.Error(
                "Snapshot button has no WorldViewModel.");

            return;
        }

        DebugConsole.Log(
            $"Snapshot requested for world: {worldViewModel.Name}");

        try
        {
            var world =
                await _worldAppService.GetWorldByIdAsync(
                    worldViewModel.Id);

            if (world is null)
            {
                throw new InvalidOperationException(
                    $"World '{worldViewModel.Id}' was not found.");
            }

            DebugConsole.Log(
                $"Checking changes for world: {world.Name}");

            var message =
                $"Snapshot {DateTime.Now:dd.MM.yyyy HH:mm:ss}";

            DebugConsole.Log(
                $"Creating snapshot with message: {message}");

            var snapshot =
                await _snapshotAppService.CreateSnapshotAsync(
                    world,
                    _localPlayerId,
                    message);

            DebugConsole.Log(
                $"Snapshot #{snapshot.Version} created successfully.");

            MessageBox.Show(
                $"Создан {snapshot.VersionText}\n\n" +
                $"Hash: {snapshot.ShortHash}\n" +
                $"Дата: {snapshot.CreatedText}",
                "Snapshot создан",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            await LoadWorldsAsync();
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to create snapshot: {exception}");

            MessageBox.Show(
                exception.Message,
                "Ошибка создания snapshot",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;

        DebugConsole.Log("MainWindow loaded.");

        await LoadWorldsAsync();
    }

    private async Task LoadWorldsAsync()
    {
        DebugConsole.Log("Loading worlds...");

        try
        {
            var worlds =
                await _worldAppService.GetWorldsAsync();

            _worlds.Clear();
            _worlds.AddRange(worlds);

            DebugConsole.Log(
                $"Loaded worlds: {_worlds.Count}");

            RefreshWorldList();
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to load worlds: {exception}");

            MessageBox.Show(
                exception.Message,
                "Ошибка загрузки миров",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void AddWorldButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log(
            "Add world requested.");

        using var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true,
            Title = "Выберите папку Minecraft мира"
        };

        if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
        {
            DebugConsole.Log(
                "World selection cancelled.");

            return;
        }

        var worldPath = dialog.FileName;

        DebugConsole.Log(
            $"Selected world path: {worldPath}");

        if (!Directory.Exists(worldPath))
        {
            DebugConsole.Error(
                $"Selected directory does not exist: {worldPath}");

            MessageBox.Show(
                "Выбранная папка не существует.",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        try
        {
            var worldName =
                Path.GetFileName(
                    worldPath.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));

            if (string.IsNullOrWhiteSpace(worldName))
            {
                worldName = "Minecraft World";
            }

            DebugConsole.Log(
                $"World name: {worldName}");

            DebugConsole.Log(
                "Creating world...");

            var world =
                await _worldAppService.AddWorldAsync(
                    name: worldName,
                    localPath: worldPath,
                    minecraftVersion: "Unknown",
                    loader: "Unknown");

            DebugConsole.Log(
                $"World created successfully. ID: {world.Id}");

            _worlds.Add(world);

            RefreshWorldList();

            DebugConsole.Log(
                $"World added to UI. Total worlds: {_worlds.Count}");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to add world: {exception}");

            MessageBox.Show(
                exception.Message,
                "Не удалось добавить мир",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RefreshWorldList()
    {
        DebugConsole.Log(
            $"Refreshing world list. Count: {_worlds.Count}");

        if (_worlds.Count == 0)
        {
            EmptyState.Visibility =
                Visibility.Visible;

            WorldsList.Visibility =
                Visibility.Collapsed;

            DebugConsole.Log(
                "World list is empty.");

            return;
        }

        EmptyState.Visibility =
            Visibility.Collapsed;

        WorldsList.Visibility =
            Visibility.Visible;

        WorldsList.DataContext =
            _worlds;

        DebugConsole.Log(
            "World list refreshed.");
    }
}