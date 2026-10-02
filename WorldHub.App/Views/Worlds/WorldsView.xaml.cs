using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Services.Application;
using WorldHub.App.Services.Diagnostics;
using WorldHub.App.ViewModels.Worlds;
using WorldHub.App.Views.Dialogs;
using WorldHub.Sync.Transfer;

namespace WorldHub.App.Views.Worlds;

public delegate bool FriendEndpointProvider(
    out string host,
    out int port);

public partial class WorldsView : UserControl
{
    private WorldAppService? _worldAppService;
    private WorldTransferOrchestrator? _worldTransferOrchestrator;
    private string? _dataPath;

    private FriendEndpointProvider? _tryGetFriendEndpoint;
    private Func<Task>? _refreshRequested;
    private Action? _goToConnectionRequested;

    public List<WorldViewModel> Worlds { get; } = [];

    public WorldsView()
    {
        InitializeComponent();
    }

     public void Initialize(
         WorldAppService worldAppService,
         WorldTransferOrchestrator worldTransferOrchestrator,
         string dataPath,
         FriendEndpointProvider tryGetFriendEndpoint,
         Func<Task> refreshRequested,
         Action goToConnectionRequested)
    {
        ArgumentNullException.ThrowIfNull(worldAppService);
        ArgumentNullException.ThrowIfNull(worldTransferOrchestrator);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPath);
        ArgumentNullException.ThrowIfNull(tryGetFriendEndpoint);
        ArgumentNullException.ThrowIfNull(refreshRequested);
        ArgumentNullException.ThrowIfNull(goToConnectionRequested);

        _worldAppService = worldAppService;
        _worldTransferOrchestrator = worldTransferOrchestrator;
        _dataPath = Path.GetFullPath(dataPath);
        _tryGetFriendEndpoint = tryGetFriendEndpoint;
        _refreshRequested = refreshRequested;
        _goToConnectionRequested = goToConnectionRequested;
    }

    public async Task RefreshAsync()
    {
        if (_worldAppService is null)
        {
            return;
        }

        DebugConsole.Log("Loading worlds...");

        try
        {
            var worlds =
                await _worldAppService.GetWorldsAsync();

            Worlds.Clear();
            Worlds.AddRange(worlds);

            DebugConsole.Log(
                $"Loaded worlds: {Worlds.Count}");

            RefreshWorldList();
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to load worlds: {exception}");

            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка загрузки миров",
                exception.Message);
        }
    }

    private void RefreshWorldList()
    {
        DebugConsole.Log(
            $"Refreshing world list. Count: {Worlds.Count}");

        if (Worlds.Count == 0)
        {
            EmptyState.Visibility =
                Visibility.Visible;

            WorldsList.Visibility =
                Visibility.Collapsed;

            WorldsItemsControl.ItemsSource = null;

            DebugConsole.Log(
                "World list is empty.");

            return;
        }

        EmptyState.Visibility =
            Visibility.Collapsed;

        WorldsList.Visibility =
            Visibility.Visible;

        WorldsItemsControl.ItemsSource =
            Worlds.ToList();

        DebugConsole.Log(
            "World list refreshed.");
    }

    private async void AddWorldButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_worldAppService is null)
        {
            return;
        }

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

            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка",
                "Выбранная папка не существует.");

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

            Worlds.Add(world);

            RefreshWorldList();

            DebugConsole.Log(
                $"World added to UI. Total worlds: {Worlds.Count}");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to add world: {exception}");

            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Не удалось добавить мир",
                exception.Message);
        }
    }

    private void GoToConnectionButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _goToConnectionRequested?.Invoke();
    }

    private async void PushWorldButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_worldAppService is null ||
            _worldTransferOrchestrator is null ||
            _tryGetFriendEndpoint is null)
        {
            return;
        }

        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is not WorldViewModel worldViewModel)
        {
            return;
        }

        if (!_tryGetFriendEndpoint(
                out var host,
                out var port))
        {
            DialogWindow.ShowWarning(
                Window.GetWindow(this)!,
                "Отправка мира",
                "Сначала укажите IP-адрес и порт другого WorldHub " +
                "на странице подключения.");

            return;
        }

        var world =
            await _worldAppService.GetWorldByIdAsync(
                worldViewModel.Id);

        if (world is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Отправка мира",
                "Мир не найден.");

            return;
        }

        if (world.CurrentSnapshotId <= 0)
        {
            DialogWindow.ShowWarning(
                Window.GetWindow(this)!,
                "Отправка мира",
                $"У мира «{world.Name}» нет snapshot.\n\n" +
                "Сначала создайте snapshot.");

            return;
        }

        DebugConsole.Log(
            $"Push requested: {world.Name} -> {host}:{port}");

        try
        {
            var progress =
                new Progress<long>(
                    transferredBytes =>
                    {
                        DebugConsole.Log(
                            $"Push progress: {transferredBytes:N0} bytes.");
                    });

            await _worldTransferOrchestrator.PushAsync(
                world,
                host,
                port,
                progress);

            DialogWindow.ShowInformation(
                Window.GetWindow(this)!,
                "Отправка завершена",
                $"Мир «{world.Name}» успешно отправлен.\n\n" +
                $"Получатель: {host}:{port}");

            DebugConsole.Log(
                $"Push completed: {world.Name} -> {host}:{port}");
        }
        catch (OperationCanceledException)
        {
            DebugConsole.Log(
                $"Push cancelled: {world.Name}");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Push failed for world '{world.Name}': {exception}");

            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка отправки",
                $"Не удалось отправить мир.\n\n{exception.Message}");
        }
    }

    private async void PullWorldButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_worldAppService is null ||
            _worldTransferOrchestrator is null ||
            _dataPath is null ||
            _tryGetFriendEndpoint is null)
        {
            return;
        }

        if (sender is not FrameworkElement element)
        {
            DebugConsole.Error(
                "Pull button sender is invalid.");

            return;
        }

        if (element.DataContext is not WorldViewModel worldViewModel)
        {
            DebugConsole.Error(
                "Pull button has no WorldViewModel.");

            return;
        }

        if (!_tryGetFriendEndpoint(
                out var host,
                out var port))
        {
            DialogWindow.ShowWarning(
                Window.GetWindow(this)!,
                "Получение мира",
                "Сначала укажите IP-адрес и порт другого WorldHub " +
                "на странице подключения.");

            return;
        }

        var world =
            await _worldAppService.GetWorldByIdAsync(
                worldViewModel.Id);

        if (world is null)
        {
            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Получение мира",
                "Мир не найден.");

            return;
        }

        string? targetPath = null;

        var receivedWorldsRoot =
            Path.Combine(
                _dataPath,
                "received-worlds");

        var isTemporaryWorld =
            Path.GetFullPath(world.LocalPath)
                .StartsWith(
                    Path.GetFullPath(receivedWorldsRoot) +
                    Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);

        if (isTemporaryWorld)
        {
            using var dialog =
                new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Multiselect = false,
                    Title = "Выберите папку Minecraft мира"
                };

            if (dialog.ShowDialog() !=
                CommonFileDialogResult.Ok)
            {
                DebugConsole.Log(
                    $"Pull cancelled: {world.Name}");

                return;
            }

            targetPath =
                dialog.FileName;
        }

        DebugConsole.Log(
            $"Pull requested: {world.Name} <- {host}:{port}");

        try
        {
            var progress =
                new Progress<long>(
                    transferredBytes =>
                    {
                        DebugConsole.Log(
                            $"Pull progress: {transferredBytes:N0} bytes.");
                    });

            await _worldTransferOrchestrator.PullAsync(
                world,
                host,
                port,
                targetPath,
                progress);

            if (_refreshRequested is not null)
            {
                await _refreshRequested();
            }

            DialogWindow.ShowInformation(
                Window.GetWindow(this)!,
                "Получение завершено",
                $"Мир «{world.Name}» успешно получен.");

            DebugConsole.Log(
                $"Pull completed: {world.Name} <- {host}:{port}");
        }
        catch (OperationCanceledException)
        {
            DebugConsole.Log(
                $"Pull cancelled: {world.Name}");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Pull failed for world '{world.Name}': {exception}");

            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка получения",
                $"Не удалось получить мир.\n\n{exception.Message}");
        }
    }
}