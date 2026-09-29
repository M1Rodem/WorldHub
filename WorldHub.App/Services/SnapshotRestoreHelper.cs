using System.Windows;
using WorldHub.App.ViewModels;
using WorldHub.Core.Entities;

namespace WorldHub.App.Services;

public static class SnapshotRestoreHelper
{
    public static async Task<bool> RestoreAsync(
        Window owner,
        World world,
        SnapshotViewModel snapshot,
        SnapshotAppService snapshotAppService)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshotAppService);

        var result =
            MessageBox.Show(
                owner,
                $"Восстановить {snapshot.VersionText}?\n\n" +
                $"Дата: {snapshot.CreatedText}\n" +
                $"Hash: {snapshot.ShortHash}\n\n" +
                "Текущее состояние мира будет заменено выбранной версией.",
                "Восстановление мира",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            DebugConsole.Log(
                $"Restore requested: snapshot #{snapshot.Version}, world: {world.Name}");

            await snapshotAppService.RestoreAsync(
                world,
                snapshot.Id);

            DebugConsole.Log(
                $"Snapshot #{snapshot.Version} restored successfully.");

            MessageBox.Show(
                owner,
                $"Мир восстановлен из {snapshot.VersionText}.",
                "Восстановление завершено",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return true;
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to restore snapshot #{snapshot.Version}: {exception}");

            MessageBox.Show(
                owner,
                exception.Message,
                "Ошибка восстановления",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }
}