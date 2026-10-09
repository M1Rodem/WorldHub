using WorldHub.Core.Enums;

namespace WorldHub.Core.Entities;

public sealed class WorldHubParticipant
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string IpAddress { get; set; }

    public string? DeviceId { get; set; }

    public string? UserName { get; set; }

    public string? PcName { get; set; }

    public string? WorldHubVersion { get; set; }

    public string? WorldHubStatus { get; set; }

    public string? GoogleDriveStatus { get; set; }

    /// <summary>
    /// Google-email участника. Заполняется при успешной проверке
    /// из PeerInfo. Используется для приглашения в общую папку.
    /// </summary>
    public string? GoogleEmail { get; set; }

    public DateTimeOffset? LastSeenAtUtc { get; set; }

    public DateTimeOffset? LastCheckAtUtc { get; set; }

    public bool IsPingAvailable { get; set; }

    public bool IsWorldHubResponding { get; set; }

    public FolderAccessStatus FolderAccessStatus { get; set; } =
        FolderAccessStatus.Unknown;

    public DateTimeOffset? LastFolderAccessCheckAtUtc { get; set; }
}