namespace WorldHub.Updater.Models;

public sealed record UpdateCheckOutput(
    bool Success,
    string CurrentVersion,
    string LatestVersion,
    bool UpdateAvailable,
    string? DownloadUrl,
    string? Error);