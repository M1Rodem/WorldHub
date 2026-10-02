using WorldHub.Updater.Models;
using System.Diagnostics;
namespace WorldHub.Updater.Services;

public sealed class UpdateRunner
{
    private const string WindowsPackagePrefix = "WorldHub-portable-v";
    private const string WindowsPackageSuffix = ".zip";
    private readonly GitHubReleaseClient _releaseClient;
    private readonly UpdateDownloader _downloader;
    private readonly UpdateInstaller _installer;
    public UpdateRunner(
        GitHubReleaseClient releaseClient,
        UpdateDownloader downloader,
        UpdateInstaller installer)
    {
        ArgumentNullException.ThrowIfNull(releaseClient);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(installer);

        _releaseClient = releaseClient;
        _downloader = downloader;
        _installer = installer;
    }


    public async Task RunAsync(
        UpdateArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);


        Console.WriteLine(
            "Checking latest WorldHub release...");


        var checker =
            new UpdateChecker(_releaseClient);


        var result =
            await checker.CheckAsync(
                arguments.CurrentVersion,
                cancellationToken);


        if (!result.UpdateAvailable)
        {
            Console.WriteLine(
                "WorldHub is already up to date.");

            return;
        }


        var release = result.Release;

        if (release is null)
        {
            throw new InvalidOperationException(
                "Release information is missing.");
        }

        var package =
            release.Assets.FirstOrDefault(asset =>
                asset.Name.StartsWith(
                    WindowsPackagePrefix,
                    StringComparison.OrdinalIgnoreCase)
                &&
                asset.Name.EndsWith(
                    WindowsPackageSuffix,
                    StringComparison.OrdinalIgnoreCase));

        if (package is null)
        {
            throw new InvalidOperationException(
                "Windows update package was not found.");
        }

        Console.WriteLine(
            $"Downloading: {package.Name}");

        var tempPackage =
            Path.Combine(
                Path.GetTempPath(),
                "WorldHubUpdate",
                package.Name);



        await _downloader.DownloadAsync(
            package.DownloadUrl,
            tempPackage,
            cancellationToken);



        Console.WriteLine(
            "Download completed.");



        Console.WriteLine(
            "Waiting for WorldHub to close...");

        await WaitForProcessExitAsync(
            arguments.ProcessId,
            cancellationToken);

        Console.WriteLine(
            "Installing update...");

        _installer.Install(
            tempPackage,
            arguments.ApplicationPath);



        Console.WriteLine(
            "Update installed successfully.");
    }

    private static async Task WaitForProcessExitAsync(
        int processId,
        CancellationToken cancellationToken)
    {
        try
        {
            var process =
                Process.GetProcessById(processId);


            while (!process.HasExited)
            {
                await Task.Delay(
                    500,
                    cancellationToken);
            }
        }
        catch (ArgumentException)
        {
            // Процесс уже закрыт
        }
    }
}