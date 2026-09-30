namespace WorldHub.Updater.Models;

public sealed record GitHubRelease(
    string TagName,
    string Name,
    bool IsPrerelease,
    IReadOnlyList<GitHubReleaseAsset> Assets);

public sealed record GitHubReleaseAsset(
    string Name,
    string DownloadUrl);