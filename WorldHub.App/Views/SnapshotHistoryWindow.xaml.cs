using System.Windows;
using WorldHub.App.Services;
using WorldHub.App.ViewModels;
using WorldHub.Core.Entities;

namespace WorldHub.App.Views;

public partial class SnapshotHistoryWindow : Window
{
    private readonly SnapshotAppService _snapshotAppService;

    private readonly World _world;

    public SnapshotHistoryWindow(
        World world,
        SnapshotAppService snapshotAppService)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(snapshotAppService);

        _world = world;
        _snapshotAppService = snapshotAppService;

        InitializeComponent();

        WorldNameText.Text =
            $"История — {_world.Name}";

        Loaded += SnapshotHistoryWindow_Loaded;
    }

    private async void SnapshotHistoryWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= SnapshotHistoryWindow_Loaded;

        await LoadHistoryAsync();
    }
    private async void RestoreButton_Click(
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

        var restored =
            await SnapshotRestoreHelper.RestoreAsync(
                this,
                _world,
                snapshot,
                _snapshotAppService);

        if (!restored)
        {
            return;
        }

        await LoadHistoryAsync();
    }
    private async Task LoadHistoryAsync()
    {
        try
        {
            DebugConsole.Log(
                $"Loading snapshot history for world: {_world.Name}");

            var snapshots =
                await _snapshotAppService.GetHistoryAsync(
                    _world.Id);

            SnapshotsList.ItemsSource = snapshots;

            DebugConsole.Log(
                $"Loaded snapshots: {snapshots.Count}");
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to load snapshot history: {exception}");

            MessageBox.Show(
                exception.Message,
                "Ошибка загрузки истории",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}