using System.ComponentModel;
using System.Runtime.CompilerServices;
using WorldHub.Core.Entities;

namespace WorldHub.App.ViewModels.Worlds;

public sealed class WorldViewModel : INotifyPropertyChanged
{
    private bool _isSelected;
    public long CurrentSnapshotVersion { get; }
    public WorldViewModel(
        World world,
        long currentSnapshotVersion = 0)
    {
        Id = world.Id;
        Name = world.Name;
        LocalPath = world.LocalPath;
        MinecraftVersion = world.MinecraftVersion;
        Loader = world.Loader;
        LoaderVersion = world.LoaderVersion;
        CurrentSnapshotId = world.CurrentSnapshotId;
        CurrentSnapshotVersion = currentSnapshotVersion;
        Status = world.Status.ToString();
    }

    public Guid Id { get; }

    public string Name { get; }

    public string LocalPath { get; }

    public string MinecraftVersion { get; }

    public string Loader { get; }

    public string? LoaderVersion { get; }

    public long CurrentSnapshotId { get; }

    public string Status { get; }

    public string StatusText =>
        Status switch
        {
            "Ready" => "Готов",
            "Playing" => "Играется",
            "Syncing" => "Синхронизация",
            "Restoring" => "Восстановление",
            "Error" => "Ошибка",
            _ => Status
        };

    public string VersionText =>
        CurrentSnapshotVersion > 0
            ? $"Снимок #{CurrentSnapshotVersion}"
            : "Нет snapshot";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}