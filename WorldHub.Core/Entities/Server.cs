namespace WorldHub.Core.Entities;

public sealed class Server
{
    public Guid Id { get; init; }
    public required string Name { get; set; }
    public required string LocalPath { get; set; }
    public required string MinecraftVersion { get; set; }
    public required string Loader { get; set; }
    public string? LoaderVersion { get; set; }
    public Guid? WorldHubServerId { get; set; }
    public required ServerLaunchConfiguration LaunchConfiguration { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; set; }
}