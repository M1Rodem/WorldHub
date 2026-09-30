using System.Diagnostics;
using System.IO;

namespace WorldHub.App.Services;

public sealed class UpdaterProcessService
{
    private const string UpdaterFileName = "WorldHub.Updater.exe";

    public string GetUpdaterPath()
    {
        var applicationDirectory =
            AppContext.BaseDirectory;

        var updaterPath =
            Path.Combine(
                applicationDirectory,
                UpdaterFileName);

        return updaterPath;
    }

    public bool IsUpdaterAvailable()
    {
        return File.Exists(GetUpdaterPath());
    }

    public Process StartCheck(string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new ArgumentException(
                "Current version cannot be empty.",
                nameof(currentVersion));
        }

        var updaterPath = GetUpdaterPath();

        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException(
                "WorldHub updater was not found.",
                updaterPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = updaterPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        startInfo.ArgumentList.Add("--check");
        startInfo.ArgumentList.Add("--version");
        startInfo.ArgumentList.Add(currentVersion);

        var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException(
                "Failed to start WorldHub updater.");
        }

        return process;
    }
}