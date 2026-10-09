using WorldHub.Core.Enums;

namespace WorldHub.Core.Entities;

public sealed class ServerLaunchConfiguration
{
    public ServerLaunchType Type { get; set; }

    public required string FilePath { get; set; }

    public string? JavaPath { get; set; }

    public string? JvmArguments { get; set; }

    public string? ProgramArguments { get; set; }
}