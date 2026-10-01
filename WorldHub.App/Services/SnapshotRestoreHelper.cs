using System.Windows;
using WorldHub.App.ViewModels;
using WorldHub.App.Views;
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

        if (!DialogWindow.ShowConfirmation(
            owner,
            "Восстановление мира",
            $"Восстановить {snapshot.VersionText}?\n\n" +
            $"Дата: {snapshot.CreatedText}\n" +
            $"Hash: {snapshot.ShortHash}\n\n" +
            "Текущее состояние мира будет заменено выбранной версией."))
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

            DialogWindow.ShowInformation(
                owner,
                "Восстановление завершено",
                $"Мир восстановлен из {snapshot.VersionText}.");

            return true;
        }
        catch (Exception exception)
        {
            DebugConsole.Error(
                $"Failed to restore snapshot #{snapshot.Version}: {exception}");

            DialogWindow.ShowError(
                owner,
                "Ошибка восстановления",
                exception.Message);

            return false;
        }
    }
}