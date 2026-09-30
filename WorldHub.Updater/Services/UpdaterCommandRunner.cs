using System.Text.Json;
using WorldHub.Updater.Models;

namespace WorldHub.Updater.Services;

public sealed class UpdaterCommandRunner
{
    private const string WindowsPackagePrefix = "WorldHub-v";
    private const string WindowsPackageSuffix = "-win-x64.zip";

    private readonly GitHubReleaseClient _releaseClient;

    public UpdaterCommandRunner(GitHubReleaseClient releaseClient)
    {
        ArgumentNullException.ThrowIfNull(releaseClient);

        _releaseClient = releaseClient;
    }

    public async Task<int> CheckAsync(
        string currentVersion,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var checker = new UpdateChecker(_releaseClient);

            var result = await checker.CheckAsync(
                currentVersion,
                cancellationToken);

            var downloadUrl = result.Release?.Assets
                .FirstOrDefault(asset =>
                    asset.Name.StartsWith(
                        WindowsPackagePrefix,
                        StringComparison.OrdinalIgnoreCase) &&
                    asset.Name.EndsWith(
                        WindowsPackageSuffix,
                        StringComparison.OrdinalIgnoreCase))
                ?.DownloadUrl;

            if (result.UpdateAvailable &&
                string.IsNullOrWhiteSpace(downloadUrl))
            {
                throw new InvalidOperationException(
                    "A new version is available, but the Windows x64 update package was not found.");
            }

            var output = new UpdateCheckOutput(
                Success: true,
                CurrentVersion: result.CurrentVersion,
                LatestVersion: result.LatestVersion,
                UpdateAvailable: result.UpdateAvailable,
                DownloadUrl: downloadUrl,
                Error: null);

            Console.WriteLine(
                JsonSerializer.Serialize(output));

            return 0;
        }
        catch (Exception exception)
        {
            var output = new UpdateCheckOutput(
                Success: false,
                CurrentVersion: currentVersion,
                LatestVersion: currentVersion,
                UpdateAvailable: false,
                DownloadUrl: null,
                Error: exception.Message);

            Console.WriteLine(
                JsonSerializer.Serialize(output));

            return 1;
        }
    }
}