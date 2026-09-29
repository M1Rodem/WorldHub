using System.Windows;
using System.Windows.Controls;
using WorldHub.App.ViewModels;
using WorldHub.Sync.Services;

namespace WorldHub.App.Views;

public partial class SettingsView : UserControl
{
    private WorldDeletionService? _worldDeletionService;
    private List<WorldViewModel>? _worlds;
    private Func<Task>? _dataChangedHandler;

    public SettingsView()
    {
        InitializeComponent();
    }

    public void Initialize(
        WorldDeletionService worldDeletionService,
        List<WorldViewModel> worlds,
        Func<Task>? dataChangedHandler = null)
    {
        ArgumentNullException.ThrowIfNull(worldDeletionService);
        ArgumentNullException.ThrowIfNull(worlds);

        _worldDeletionService = worldDeletionService;
        _worlds = worlds;
        _dataChangedHandler = dataChangedHandler;

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
}