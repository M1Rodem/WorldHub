using System.Net.Http.Json;
using System.Text.Json.Serialization;
using WorldHub.Updater.Models;

namespace WorldHub.Updater.Services;

public sealed class GitHubReleaseClient
{
    private const string RepositoryOwner = "M1Rodem";
    private const string RepositoryName = "WorldHub";

    private static readonly Uri LatestReleaseUri = new(
        $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest");

    private readonly HttpClient _httpClient;

    public GitHubReleaseClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
    }

    public async Task<GitHubRelease> GetLatestReleaseAsync(
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            LatestReleaseUri,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GitHubReleaseResponse>(
            cancellationToken);

        if (release is null)
        {
            throw new InvalidOperationException(
                "GitHub returned an empty release response.");
        }

        if (string.IsNullOrWhiteSpace(release.TagName))
        {
            throw new InvalidOperationException(
                "GitHub release does not contain a tag name.");
        }

        var assets = release.Assets
            .Where(asset =>
                !string.IsNullOrWhiteSpace(asset.Name) &&
                !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            .Select(asset =>
                new GitHubReleaseAsset(
                    asset.Name!,
                    asset.BrowserDownloadUrl!))
            .ToList();

        return new GitHubRelease(
            release.TagName,
            release.Name ?? release.TagName,
            release.Prerelease,
            assets);
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAssetResponse> Assets { get; init; } = [];
    }

    private sealed class GitHubReleaseAssetResponse
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; init; }
    }
}