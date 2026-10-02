using System.Windows;
using WorldHub.App.Helpers;
using WorldHub.App.Services.Application;
using WorldHub.App.Services.Diagnostics;
using WorldHub.App.ViewModels.History;
using WorldHub.Core.Entities;
using WorldHub.App.Views.Dialogs;

namespace WorldHub.App.Views.History;

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

            DialogWindow.ShowError(
                this,
                "Ошибка загрузки истории",
                exception.Message);
        }
    }
}