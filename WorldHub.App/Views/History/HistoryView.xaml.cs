using System.Windows;
using System.Windows.Controls;
using WorldHub.App.Helpers;
using WorldHub.App.Services.Application;
using WorldHub.App.Services.Diagnostics;
using WorldHub.App.ViewModels.History;
using WorldHub.App.Views.Dialogs;

namespace WorldHub.App.Views.History;

public partial class HistoryView : UserControl
{
    private WorldAppService? _worldAppService;
    private SnapshotAppService? _snapshotAppService;
    private Func<Task>? _refreshRequested;

    private readonly List<WorldHistoryViewModel> _worldHistory = [];

    public HistoryView()
    {
        InitializeComponent();
    }

    public void Initialize(
        WorldAppService worldAppService,
        SnapshotAppService snapshotAppService,
        Func<Task> refreshRequested)
    {
        ArgumentNullException.ThrowIfNull(worldAppService);
        ArgumentNullException.ThrowIfNull(snapshotAppService);
        ArgumentNullException.ThrowIfNull(refreshRequested);

        _worldAppService = worldAppService;
        _snapshotAppService = snapshotAppService;
        _refreshRequested = refreshRequested;
    }

    public async Task RefreshAsync()
    {
        if (_worldAppService is null ||
            _snapshotAppService is null)
        {
            return;
        }

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
                Window.GetWindow(this)!,
                "Ошибка загрузки истории",
                exception.Message);
        }
    }

    private async void RestoreSnapshotButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_worldAppService is null ||
            _snapshotAppService is null)
        {
            return;
        }

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
                    Window.GetWindow(this)!,
                    world,
                    snapshot,
                    _snapshotAppService);

            if (!restored)
            {
                return;
            }

            await RefreshAsync();

            if (_refreshRequested is not null)
            {
                await _refreshRequested();
            }
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to prepare snapshot restore: {exception}");

            DialogWindow.ShowError(
                Window.GetWindow(this)!,
                "Ошибка восстановления",
                exception.Message);
        }
    }
}