using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using System.Windows;
using WorldHub.App.Services;
using WorldHub.App.ViewModels;
using WorldHub.App.Views;
using WorldHub.Sync.Services;

namespace WorldHub.App;

public partial class MainWindow
{
    private readonly WorldAppService _worldAppService;
    private readonly List<WorldHistoryViewModel> _worldHistory = [];
    private readonly SnapshotAppService _snapshotAppService;
    private readonly WorldDeletionService _worldDeletionService;
    private readonly List<WorldViewModel> _worlds = [];
    private readonly LocalPlayerIdentity _localPlayerIdentity;
    private readonly WorldHubNetworkService _worldHubNetworkService;
    private readonly WorldHubTransferService _worldHubTransferService;
    private readonly string _dataPath;
    public MainWindow(
        WorldAppService worldAppService,
        SnapshotAppService snapshotAppService,
        WorldDeletionService worldDeletionService,
        LocalPlayerIdentity localPlayerIdentity,
        WorldHubNetworkService worldHubNetworkService,
        WorldHubTransferService worldHubTransferService,
        string dataPath)
    {
        ArgumentNullException.ThrowIfNull(worldAppService);
        ArgumentNullException.ThrowIfNull(snapshotAppService);
        ArgumentNullException.ThrowIfNull(worldDeletionService);
        ArgumentNullException.ThrowIfNull(localPlayerIdentity);
        ArgumentNullException.ThrowIfNull(worldHubNetworkService);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataPath);

        DebugConsole.Log("Creating MainWindow...");

        InitializeComponent();

        DebugConsole.Log("MainWindow UI initialized.");

        _worldAppService = worldAppService;
        _snapshotAppService = snapshotAppService;
        _worldDeletionService = worldDeletionService;
        _localPlayerIdentity = localPlayerIdentity;
        _worldHubNetworkService = worldHubNetworkService;
        _worldHubTransferService = worldHubTransferService;
        _dataPath = Path.GetFullPath(dataPath);

        ConnectionPage.Initialize(_worldHubNetworkService);

        var appVersionService = new AppVersionService();
        var appSettingsService = new AppSettingsService();

        SettingsPage.Initialize(
            _worldDeletionService,
            _worlds,
            RefreshDataAsync,
            appVersionService,
            appSettingsService);

        DebugConsole.Log(
            "Application services initialized.");

        Loaded += MainWindow_Loaded;
    }
    private async Task LoadHistoryAsync()
    {
        DebugConsole.Log("Loading snapshot history...");

        try
        {
            var history = new List<WorldHistoryViewModel>();

            var worlds =
                await _worldAppService.GetWorldsAsync();

            foreach (var world in worlds)
            {
                var snapshots =
                    await _snapshotAppService.GetHistoryAsync(
                        world.Id);

                history.Add(
                    new WorldHistoryViewModel(
                        world,
                        snapshots));
            }

            _worldHistory.Clear();
            _worldHistory.AddRange(history);

            HistoryWorldsList.ItemsSource = null;
            HistoryWorldsList.ItemsSource = _worldHistory;

            DebugConsole.Log(
                $"Loaded history for {_worldHistory.Count} worlds.");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to load snapshot history: {exception}");

            DialogWindow.ShowError(
                this,
                "Ошибка загрузки истории",
                exception.Message);
        }
    }

    private void ConnectionNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log(
            "Connection page requested.");

        WorldsPage.Visibility =
            Visibility.Collapsed;

        HistoryPage.Visibility =
            Visibility.Collapsed;

        SettingsPage.Visibility =
            Visibility.Collapsed;

        ConnectionPage.Visibility =
            Visibility.Visible;

        PageTitleText.Text =
            "Подключение";

        PageDescriptionText.Text =
            "Подключение к другому WorldHub по IP-адресу и порту.";
    }

    private async void HistoryNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log("History page requested.");

        WorldsPage.Visibility =
            Visibility.Collapsed;

        SettingsPage.Visibility =
            Visibility.Collapsed;

        HistoryPage.Visibility =
            Visibility.Visible;

        ConnectionPage.Visibility =
            Visibility.Collapsed;

        PageTitleText.Text =
            "История";

        PageDescriptionText.Text =
            "История локальных версий миров";

        await LoadHistoryAsync();
    }

    private void MyWorldsButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log("My worlds page requested.");

        ConnectionPage.Visibility =
            Visibility.Collapsed;

        HistoryPage.Visibility =
            Visibility.Collapsed;

        SettingsPage.Visibility =
            Visibility.Collapsed;

        WorldsPage.Visibility =
            Visibility.Visible;

        PageTitleText.Text =
            "Мои миры";

        PageDescriptionText.Text =
            "Управляйте мирами, версиями и подключениями";
    }

    private void SettingsNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        DebugConsole.Log("Settings page requested.");

        WorldsPage.Visibility =
            Visibility.Collapsed;

        HistoryPage.Visibility =
            Visibility.Collapsed;

        SettingsPage.Visibility =
            Visibility.Visible;

        ConnectionPage.Visibility =
            Visibility.Collapsed;

        PageTitleText.Text =
            "Настройки";

        PageDescriptionText.Text =
            "Управление WorldHub, мирами и локальными данными";

        SettingsPage.RefreshWorlds(_worlds);
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
                    _localPlayerIdentity.PlayerId,
                    message);

            DebugConsole.Log(
                $"Snapshot #{snapshot.Version} created successfully.");

            DialogWindow.ShowInformation(
                this,
                "Snapshot создан",
                $"Создан {snapshot.VersionText}\n\n" +
                $"Hash: {snapshot.ShortHash}\n" +
                $"Дата: {snapshot.CreatedText}");

            await LoadWorldsAsync();
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to create snapshot: {exception}");

            DialogWindow.ShowError(
                this,
                "Ошибка создания snapshot",
                exception.Message);
        }
    }

    private async void HistoryButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            DebugConsole.Error(
                "History button sender is invalid.");

            return;
        }

        if (element.DataContext is not WorldViewModel worldViewModel)
        {
            DebugConsole.Error(
                "History button has no WorldViewModel.");

            return;
        }

        DebugConsole.Log(
            $"History requested for world: {worldViewModel.Name}");

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

            var window =
                new Views.SnapshotHistoryWindow(
                    world,
                    _snapshotAppService)
                {
                    Owner = this
                };

            window.ShowDialog();

            DebugConsole.Log(
                $"Snapshot history window closed for world: {world.Name}");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to open snapshot history: {exception}");

            DialogWindow.ShowError(
                this,
                "Ошибка открытия истории",
                exception.Message);
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

            DialogWindow.ShowError(
                this,
                "Ошибка загрузки миров",
                exception.Message);
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

            DialogWindow.ShowError(
                this,
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

            _worlds.Add(world);

            RefreshWorldList();

            DebugConsole.Log(
                $"World added to UI. Total worlds: {_worlds.Count}");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to add world: {exception}");

            DialogWindow.ShowError(
                this,
                "Не удалось добавить мир",
                exception.Message);
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

            WorldsItemsControl.ItemsSource = null;

            DebugConsole.Log(
                "World list is empty.");

            return;
        }

        EmptyState.Visibility =
            Visibility.Collapsed;

        WorldsList.Visibility =
            Visibility.Visible;

        // Передаём новый экземпляр коллекции,
        // чтобы WPF гарантированно перестроил ItemsControl.
        WorldsItemsControl.ItemsSource =
            _worlds.ToList();

        DebugConsole.Log(
            "World list refreshed.");
    }

    private async void PushWorldButton_Click(
    object sender,
    RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is not WorldViewModel worldViewModel)
        {
            return;
        }

        if (!ConnectionPage.TryGetFriendEndpoint(
                out var host,
                out var port))
        {
            DialogWindow.ShowWarning(
                this,
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
                this,
                "Отправка мира",
                "Мир не найден.");

            return;
        }

        if (world.CurrentSnapshotId <= 0)
        {
            DialogWindow.ShowWarning(
                this,
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

            await _worldHubTransferService.PushAsync(
                world,
                host,
                port,
                progress);

            DialogWindow.ShowInformation(
                this,
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
                this,
                "Ошибка отправки",
                $"Не удалось отправить мир.\n\n{exception.Message}");
        }
    }

    private async void RestoreSnapshotButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            DebugConsole.Error(
                "Restore button sender is invalid.");

            return;
        }

        if (element.DataContext is not SnapshotViewModel snapshot)
        {
            DebugConsole.Error(
                "Restore button has no SnapshotViewModel.");

            return;
        }

        try
        {
            var world =
                await _worldAppService.GetWorldByIdAsync(
                    snapshot.WorldId);

            if (world is null)
            {
                throw new InvalidOperationException(
                    $"World '{snapshot.WorldId}' was not found.");
            }

            var restored =
                await SnapshotRestoreHelper.RestoreAsync(
                    this,
                    world,
                    snapshot,
                    _snapshotAppService);

            if (!restored)
            {
                return;
            }

            await LoadHistoryAsync();
            await LoadWorldsAsync();
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to prepare snapshot restore: {exception}");

            DialogWindow.ShowError(
                this,
                "Ошибка восстановления",
                exception.Message);
        }
    }

    private async void PullWorldButton_Click(
    object sender,
    RoutedEventArgs e)
    {
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

        if (!ConnectionPage.TryGetFriendEndpoint(
                out var host,
                out var port))
        {
            DialogWindow.ShowWarning(
                this,
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
                this,
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

            await _worldHubTransferService.PullAsync(
                world,
                host,
                port,
                targetPath,
                progress);

            await LoadHistoryAsync();
            await LoadWorldsAsync();

            DialogWindow.ShowInformation(
                this,
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
                this,
                "Ошибка получения",
                $"Не удалось получить мир.\n\n{exception.Message}");
        }
    }

    private async void DeleteWorldButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            DebugConsole.Error(
                "Delete button sender is invalid.");

            return;
        }

        if (element.DataContext is not WorldViewModel worldViewModel)
        {
            DebugConsole.Error(
                "Delete button has no WorldViewModel.");

            return;
        }

        if (!DialogWindow.ShowConfirmation(
            this,
            "Удаление мира из WorldHub",
            $"Удалить мир «{worldViewModel.Name}» из WorldHub?\n\n" +
            "Все snapshots, история и связь WorldHub с этим миром будут удалены.\n\n" +
            "Физический мир Minecraft НЕ будет удалён.\n" +
            "Папка мира в Minecraft\\saves останется без изменений.\n\n" +
            "Это действие нельзя отменить."))
        {
            return;
        }

        try
        {
            DebugConsole.Log(
                $"Delete requested for world: {worldViewModel.Name}");

            await _worldDeletionService.DeleteAsync(
                worldViewModel.Id);

            _worlds.RemoveAll(
                world => world.Id == worldViewModel.Id);

            RefreshWorldList();

            if (HistoryPage.Visibility == Visibility.Visible)
            {
                await LoadHistoryAsync();
            }

            DebugConsole.Log(
                $"World deleted from WorldHub: {worldViewModel.Name}");

            DialogWindow.ShowInformation(
                this,
                "Мир удалён",
                $"Мир «{worldViewModel.Name}» удалён из WorldHub.\n\n" +
                "Физический мир Minecraft сохранён.");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to delete world '{worldViewModel.Name}': {exception}");

            DialogWindow.ShowError(
                this,
                "Ошибка удаления мира",
                exception.Message);
        }
    }

    public async Task RefreshDataAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(
                async () => await RefreshDataAsync());

            return;
        }

        await LoadWorldsAsync();

        if (HistoryPage.Visibility == Visibility.Visible)
        {
            await LoadHistoryAsync();
        }
    }
}