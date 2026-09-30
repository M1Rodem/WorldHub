using WorldHub.Updater.Models;

namespace WorldHub.Updater.Services;

public sealed class UpdateChecker
{
    private readonly GitHubReleaseClient _releaseClient;

    public UpdateChecker(GitHubReleaseClient releaseClient)
    {
        ArgumentNullException.ThrowIfNull(releaseClient);

        _releaseClient = releaseClient;
    }

    public async Task<UpdateCheckResult> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new ArgumentException(
                "Current version cannot be empty.",
                nameof(currentVersion));
        }

        var normalizedCurrentVersion =
            NormalizeVersion(currentVersion);

        var release =
            await _releaseClient.GetLatestReleaseAsync(
                cancellationToken);

        var normalizedLatestVersion =
            NormalizeVersion(release.TagName);

        var updateAvailable =
            CompareVersions(
                normalizedCurrentVersion,
                normalizedLatestVersion) < 0;

        return new UpdateCheckResult(
            normalizedCurrentVersion,
            normalizedLatestVersion,
            updateAvailable,
            release);
    }

    private static string NormalizeVersion(string version)
    {
        var normalized = version.Trim();

        if (normalized.StartsWith(
            "v",
            StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        if (!Version.TryParse(
                normalized,
                out _))
        {
            throw new FormatException(
                $"Invalid version: '{version}'.");
        }

        return normalized;
    }

    private static int CompareVersions(
        string currentVersion,
        string latestVersion)
    {
        var current =
            Version.Parse(currentVersion);

        var latest =
            Version.Parse(latestVersion);

        return current.CompareTo(latest);
    }
}