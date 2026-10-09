namespace WorldHub.Core.Entities;

public sealed class ServerDetectionResult
{
    public required string ServerDirectory { get; init; }

    public required string MinecraftVersion { get; init; }

    public required string Loader { get; init; }

    public string? LoaderVersion { get; init; }

    public required ServerLaunchConfiguration LaunchConfiguration { get; init; }
}