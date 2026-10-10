using System.Text.Json.Serialization;

namespace WorldHub.Network.Protocol;

public static class InviteProtocol
{
    public const string InviteRequestCommand = "WORLDHUB_INVITE";
    public const string InviteResponseCommand = "WORLDHUB_INVITE_RESPONSE";

    public const string ServerDeletedCommand = "WORLDHUB_SERVER_DELETED";
    public const string ServerDeletedOkCommand = "WORLDHUB_SERVER_DELETED_OK";

    public const string ParticipantLeftCommand = "WORLDHUB_PARTICIPANT_LEFT";
    public const string ParticipantLeftOkCommand = "WORLDHUB_PARTICIPANT_LEFT_OK";
}

public sealed class ServerDeletedNotification
{
    [JsonConstructor]
    public ServerDeletedNotification(
        string serverName,
        string hostDeviceId)
    {
        ServerName = serverName;
        HostDeviceId = hostDeviceId;
    }

    public string ServerName { get; init; }
    public string HostDeviceId { get; init; }
}

public sealed class ParticipantLeftNotification
{
    [JsonConstructor]
    public ParticipantLeftNotification(
        string serverName,
        string participantDeviceId,
        string participantUserName)
    {
        ServerName = serverName;
        ParticipantDeviceId = participantDeviceId;
        ParticipantUserName = participantUserName;
    }

    public string ServerName { get; init; }
    public string ParticipantDeviceId { get; init; }
    public string ParticipantUserName { get; init; }
}

public sealed class InviteRequest
{
    [JsonConstructor]
    public InviteRequest(
        string serverName,
        string? folderId,
        string? ownerEmail,
        string ownerUserName,
        string ownerDeviceId,
        List<InviteParticipant> participants)
    {
        ServerName = serverName;
        FolderId = folderId;
        OwnerEmail = ownerEmail;
        OwnerUserName = ownerUserName;
        OwnerDeviceId = ownerDeviceId;
        Participants = participants ?? [];
    }

    public string ServerName { get; init; }
    public string? FolderId { get; init; }
    public string? OwnerEmail { get; init; }
    public string OwnerUserName { get; init; }
    public string OwnerDeviceId { get; init; }
    public List<InviteParticipant> Participants { get; init; }
}

public sealed class InviteParticipant
{
    [JsonConstructor]
    public InviteParticipant(
        string deviceId,
        string ipAddress,
        string userName,
        string pcName)
    {
        DeviceId = deviceId;
        IpAddress = ipAddress;
        UserName = userName;
        PcName = pcName;
    }

    public string DeviceId { get; init; }
    public string IpAddress { get; init; }
    public string UserName { get; init; }
    public string PcName { get; init; }
}

public sealed class InviteResponse
{
    [JsonConstructor]
    public InviteResponse(bool accepted, string? message = null)
    {
        Accepted = accepted;
        Message = message;
    }

    public bool Accepted { get; init; }
    public string? Message { get; init; }
}
