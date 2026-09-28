using System.Diagnostics;

namespace WorldHub.Infrastructure.Minecraft;

public sealed class MinecraftManager
{
    private readonly string _minecraftExecutablePath;

    public MinecraftManager(string minecraftExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            minecraftExecutablePath);

        _minecraftExecutablePath = minecraftExecutablePath;
    }

    public Process Start(
        string arguments = "")
    {
        if (!File.Exists(_minecraftExecutablePath))
        {
            throw new FileNotFoundException(
                "Minecraft executable was not found.",
                _minecraftExecutablePath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _minecraftExecutablePath,
            Arguments = arguments,
            UseShellExecute = true
        };

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start Minecraft.");
    }

    public bool IsRunning()
    {
        return Process.GetProcessesByName("javaw").Length > 0
               || Process.GetProcessesByName("java").Length > 0;
    }

    public async Task WaitForExitAsync(
        Process process,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(process);

        await process.WaitForExitAsync(cancellationToken);
    }
}