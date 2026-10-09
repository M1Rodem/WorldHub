namespace WorldHub.Core.Entities;

public sealed class WorldHubServer
{
    public Guid Id { get; init; }

    public required string Name { get; set; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; }

    public List<WorldHubParticipant> Participants { get; set; } = [];

    /// <summary>
    /// ID общей папки Google Drive, привязанной к этому WorldHub-серверу.
    /// null, если папка ещё не создана.
    /// </summary>
    public string? GoogleDriveFolderId { get; set; }

    /// <summary>
    /// Email Google-аккаунта, который создал папку (владелец папки).
    /// Может совпадать с владельцем сервера, но это разные понятия.
    /// </summary>
    public string? GoogleDriveOwnerEmail { get; set; }
}