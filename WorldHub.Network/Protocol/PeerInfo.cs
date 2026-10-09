using System.Text.Json.Serialization;

namespace WorldHub.Network.Protocol;

public sealed class PeerInfo
{
    [JsonConstructor]
    public PeerInfo(
        string deviceId,
        string userName,
        string pcName,
        string worldHubVersion,
        string worldHubStatus,
        string googleDriveStatus,
        string? googleEmail)
    {
        DeviceId = deviceId;
        UserName = userName;
        PcName = pcName;
        WorldHubVersion = worldHubVersion;
        WorldHubStatus = worldHubStatus;
        GoogleDriveStatus = googleDriveStatus;
        GoogleEmail = googleEmail;
    }

    public string DeviceId { get; init; }

    public string UserName { get; init; }

    public string PcName { get; init; }

    public string WorldHubVersion { get; init; }

    public string WorldHubStatus { get; init; }

    public string GoogleDriveStatus { get; init; }

    /// <summary>
    /// Google-email текущего авторизованного аккаунта.
    /// null, если Google не подключён.
    /// </summary>
    public string? GoogleEmail { get; init; }
}