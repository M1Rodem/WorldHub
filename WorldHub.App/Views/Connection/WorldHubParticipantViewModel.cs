using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using WorldHub.Core.Entities;

namespace WorldHub.App.Views.Connection;

public sealed class WorldHubParticipantViewModel : INotifyPropertyChanged
{
    private static readonly Brush OnlineBrushValue =
        new SolidColorBrush(Color.FromRgb(0x42, 0xC7, 0x83));

    private static readonly Brush OfflineBrushValue =
        new SolidColorBrush(Color.FromRgb(0xE5, 0x6B, 0x6F));

    private static readonly Brush UnknownBrushValue =
        new SolidColorBrush(Color.FromRgb(0x8C, 0x95, 0xA3));

    private WorldHubParticipant _participant;
    private readonly string? _localDeviceId;
    private readonly bool _isCurrentLocalUserHost;

    public WorldHubParticipantViewModel(
        WorldHubParticipant participant,
        string? localDeviceId = null,
        bool isCurrentLocalUserHost = false)
    {
        ArgumentNullException.ThrowIfNull(participant);
        _participant = participant;
        _localDeviceId = localDeviceId;
        _isCurrentLocalUserHost = isCurrentLocalUserHost;
    }

    public bool IsSelf =>
        !string.IsNullOrWhiteSpace(_localDeviceId) &&
        !string.IsNullOrWhiteSpace(_participant.DeviceId) &&
        string.Equals(
            _localDeviceId,
            _participant.DeviceId,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Удалить участника из сервера может только хост, и хост не может удалить сам себя через эту кнопку (себя удаляют через удаление сервера).
    /// </summary>
    public bool CanDelete => _isCurrentLocalUserHost && !IsSelf;

    public Guid Id => _participant.Id;

    public WorldHubParticipant Participant => _participant;

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(_participant.UserName)
            ? _participant.UserName!
            : !string.IsNullOrWhiteSpace(_participant.PcName)
                ? _participant.PcName!
                : _participant.IpAddress;

    public string Subtitle
    {
        get
        {
            var pc = string.IsNullOrWhiteSpace(_participant.PcName)
                ? null
                : _participant.PcName;

            return pc is null
                ? _participant.IpAddress
                : $"{pc} · {_participant.IpAddress}";
        }
    }
    private bool HasBeenChecked => _participant.LastCheckAtUtc is not null;

    public string OnlineText =>
        !HasBeenChecked
            ? "Не проверен"
            : _participant.IsWorldHubResponding
                ? "Online"
                : _participant.IsPingAvailable
                    ? "Сеть доступна"
                    : "Offline";

    public Brush OnlineBrush =>
        !HasBeenChecked
            ? UnknownBrushValue
            : _participant.IsWorldHubResponding
                ? OnlineBrushValue
                : _participant.IsPingAvailable
                    ? UnknownBrushValue
                    : OfflineBrushValue;

    public string PingStatusText =>
        !HasBeenChecked
            ? "Не проверен"
            : _participant.IsPingAvailable
                ? "Доступен"
                : "Недоступен";

    public Brush PingBrush =>
        !HasBeenChecked
            ? UnknownBrushValue
            : _participant.IsPingAvailable
                ? OnlineBrushValue
                : OfflineBrushValue;

    public string WorldHubStatusText =>
        !HasBeenChecked
            ? "Не проверен"
            : _participant.IsWorldHubResponding
                ? "Отвечает"
                : "Не отвечает";

    public Brush WorldHubBrush =>
        !HasBeenChecked
            ? UnknownBrushValue
            : _participant.IsWorldHubResponding
                ? OnlineBrushValue
                : OfflineBrushValue;

    public string GoogleDriveStatusText
    {
        get
        {
            var status = _participant.GoogleDriveStatus;

            if (string.IsNullOrWhiteSpace(status))
            {
                return "Не проверен";
            }

            return status switch
            {
                "CheckPending" => "Проверка идёт",
                "DriveAvailable" => "Google Drive доступен",
                "TokenExpired" => "Токен недействителен",
                "NotAuthorized" => "Не авторизован",
                "NoCredentials" => "Нет credentials",
                "InsufficientPermissions" => "Недостаточно прав",
                "NetworkError" => "Нет связи",
                "Timeout" => "Нет связи",
                "Unknown" => "Не проверен",
                _ => "Неизвестно"
            };
        }
    }

    public Brush GoogleDriveBrush
    {
        get
        {
            var status = _participant.GoogleDriveStatus;

            if (string.IsNullOrWhiteSpace(status))
            {
                return UnknownBrushValue;
            }

            return status switch
            {
                "CheckPending" => UnknownBrushValue,
                "DriveAvailable" => OnlineBrushValue,
                "TokenExpired" => OfflineBrushValue,
                "InsufficientPermissions" => OfflineBrushValue,
                "NotAuthorized" => UnknownBrushValue,
                "NoCredentials" => UnknownBrushValue,
                "NetworkError" => UnknownBrushValue,
                "Timeout" => UnknownBrushValue,
                "Unknown" => UnknownBrushValue,
                _ => UnknownBrushValue
            };
        }
    }

    public string VersionText =>
        string.IsNullOrWhiteSpace(_participant.WorldHubVersion)
            ? "—"
            : _participant.WorldHubVersion!;

    public string LastSeenText
    {
        get
        {
            if (_participant.LastSeenAtUtc is null)
            {
                return "Никогда";
            }

            var delta = DateTimeOffset.UtcNow - _participant.LastSeenAtUtc.Value;

            if (delta.TotalSeconds < 60)
            {
                return "Только что";
            }

            if (delta.TotalMinutes < 60)
            {
                return $"{(int)delta.TotalMinutes} мин назад";
            }

            if (delta.TotalHours < 24)
            {
                return $"{(int)delta.TotalHours} ч назад";
            }

            return _participant.LastSeenAtUtc.Value
                .ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm");
        }
    }

    public void Update(WorldHubParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        _participant = participant;
        RaiseAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(OnlineText));
        OnPropertyChanged(nameof(OnlineBrush));
        OnPropertyChanged(nameof(PingStatusText));
        OnPropertyChanged(nameof(PingBrush));
        OnPropertyChanged(nameof(WorldHubStatusText));
        OnPropertyChanged(nameof(WorldHubBrush));
        OnPropertyChanged(nameof(GoogleDriveStatusText));
        OnPropertyChanged(nameof(GoogleDriveBrush));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(LastSeenText));
    }

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}