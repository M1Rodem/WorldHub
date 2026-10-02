using System.Diagnostics;
using System.IO;

namespace WorldHub.App.Services.Update;

public sealed class UpdaterProcessService
{
    private const string UpdaterFileName = "WorldHub.Updater.exe";


    public string GetUpdaterPath()
    {
        var applicationDirectory =
            AppContext.BaseDirectory;

        return Path.Combine(
            applicationDirectory,
            UpdaterFileName);
    }


    public bool IsUpdaterAvailable()
    {
        return File.Exists(GetUpdaterPath());
    }


    public Process StartCheck(
        string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new ArgumentException(
                "Current version cannot be empty.",
                nameof(currentVersion));
        }


        var updaterPath =
            GetUpdaterPath();


        ValidateUpdater(updaterPath);


        var startInfo =
            CreateStartInfo(updaterPath);


        startInfo.ArgumentList.Add("--check");
        startInfo.ArgumentList.Add("--version");
        startInfo.ArgumentList.Add(currentVersion);


        return StartProcess(startInfo);
    }



    public Process StartUpdate(
        string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            throw new ArgumentException(
                "Current version cannot be empty.",
                nameof(currentVersion));
        }


        var updaterPath =
            GetUpdaterPath();


        ValidateUpdater(updaterPath);


        var applicationDirectory =
            AppContext.BaseDirectory;


        var processId =
            Environment.ProcessId;


        var startInfo =
            CreateStartInfo(updaterPath);


        startInfo.ArgumentList.Add("--update");

        startInfo.ArgumentList.Add("--app");
        startInfo.ArgumentList.Add(
            applicationDirectory);

        startInfo.ArgumentList.Add("--version");
        startInfo.ArgumentList.Add(
            currentVersion);

        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(
            processId.ToString());


        return StartProcess(startInfo);
    }
    private static ProcessStartInfo CreateStartInfo(
        string updaterPath)
    {
        return new ProcessStartInfo
        {
            FileName = updaterPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }

    private static void ValidateUpdater(
        string updaterPath)
    {
        if (!File.Exists(updaterPath))
        {
            throw new FileNotFoundException(
                "WorldHub updater was not found.",
                updaterPath);
        }
    }



    private static Process StartProcess(
        ProcessStartInfo startInfo)
    {
        var process =
            Process.Start(startInfo);


        if (process is null)
        {
            throw new InvalidOperationException(
                "Failed to start WorldHub updater.");
        }


        return process;
    }
}