using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;

namespace WorldHub.App.Views.Servers;

public sealed class ServerCardViewModel : INotifyPropertyChanged
{
    private ServerStatus _status;
    private bool _isBusy;

    public ServerCardViewModel(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        Server = server;
        _status = ServerStatus.Stopped;
    }

    public Server Server { get; }

    public Guid Id => Server.Id;

    public string Name => Server.Name;

    public string LocalPath => Server.LocalPath;

    public string MinecraftVersion => Server.MinecraftVersion;

    public string Loader => Server.Loader;

    public string LoaderVersion =>
        string.IsNullOrWhiteSpace(Server.LoaderVersion)
            ? "—"
            : Server.LoaderVersion;

    // Пока Manifest/versioning ещё не реализован.
    public string SaveVersion => "v0";

    public ServerStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
                return;

            _status = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(ActionText));
            OnPropertyChanged(nameof(ActionBackground));
            OnPropertyChanged(nameof(ActionBorderBrush));
            OnPropertyChanged(nameof(ActionForeground));
            OnPropertyChanged(nameof(IsActionEnabled));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy == value)
                return;

            _isBusy = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsActionEnabled));
        }
    }

    public bool IsActionEnabled =>
        !IsBusy && ServerStatusUiHelper.CanExecuteAction(Status);

    public string StatusText => ServerStatusUiHelper.GetStatusText(Status);

    public Brush StatusBrush => ServerStatusUiHelper.GetStatusBrush(Status);

    public string ActionText =>
        Status == ServerStatus.Running
            ? "Остановить"
            : "Запустить";

    public Brush ActionBackground =>
        Status == ServerStatus.Running
            ? (Brush)(System.Windows.Application.Current?.TryFindResource("AccentRedSubtleBrush")
                ?? new SolidColorBrush(Color.FromRgb(0x22, 0x10, 0x13)))
            : (Brush)(System.Windows.Application.Current?.TryFindResource("AccentPurpleBrush")
                ?? new SolidColorBrush(Color.FromRgb(0x7C, 0x3A, 0xED)));

    public Brush ActionBorderBrush =>
        Status == ServerStatus.Running
            ? (Brush)(System.Windows.Application.Current?.TryFindResource("AccentRedBorderBrush")
                ?? new SolidColorBrush(Color.FromRgb(0x45, 0x14, 0x19)))
            : (Brush)(System.Windows.Application.Current?.TryFindResource("AccentPurpleBorderBrush")
                ?? new SolidColorBrush(Color.FromRgb(0x47, 0x24, 0x7A)));

    public Brush ActionForeground =>
        Status == ServerStatus.Running
            ? (Brush)(System.Windows.Application.Current?.TryFindResource("AccentRedBrush")
                ?? new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)))
            : (Brush)(System.Windows.Application.Current?.TryFindResource("TextPrimaryBrush")
                ?? new SolidColorBrush(Color.FromRgb(0xD5, 0xD9, 0xE0)));

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}