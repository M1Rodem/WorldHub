using System.Text.Json.Serialization;

namespace WorldHub.Network.Protocol;

public static class ServerInfoProtocol
{
    public const string ServerInfoRequest = "WORLDHUB_SERVER_INFO";
    public const string ServerInfoResponse = "WORLDHUB_SERVER_INFO_OK";
}

public sealed class ServerInfo
{
    [JsonConstructor]
    public ServerInfo(
        string? googleDriveFolderId,
        string? googleDriveOwnerEmail)
    {
        GoogleDriveFolderId = googleDriveFolderId;
        GoogleDriveOwnerEmail = googleDriveOwnerEmail;
    }

    public string? GoogleDriveFolderId { get; init; }

    public string? GoogleDriveOwnerEmail { get; init; }
}