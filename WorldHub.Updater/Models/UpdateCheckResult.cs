namespace WorldHub.Updater.Models;

public sealed record UpdateCheckResult(
    string CurrentVersion,
    string LatestVersion,
    bool UpdateAvailable,
    GitHubRelease? Release);