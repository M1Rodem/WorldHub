using System.Diagnostics;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Infrastructure.Windows;

namespace WorldHub.Infrastructure.Minecraft;

public sealed class ServerProcessLauncher
{
    public Process Launch(Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var configuration = server.LaunchConfiguration;

        return configuration.Type switch
        {
            ServerLaunchType.Script =>
                LaunchScript(server, configuration),

            ServerLaunchType.Jar =>
                LaunchJar(server, configuration),

            _ => throw new ArgumentOutOfRangeException(
                nameof(configuration.Type),
                configuration.Type,
                "Unsupported server launch type.")
        };
    }

    private static Process LaunchScript(
        Server server,
        ServerLaunchConfiguration configuration)
    {
        var scriptPath = GetAbsolutePath(
            server.LocalPath,
            configuration.FilePath);

        if (!File.Exists(scriptPath))
        {
            throw new FileNotFoundException(
                "Server launch script was not found.",
                scriptPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = server.LocalPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false
        };

        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add(scriptPath);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start Minecraft server script.");
    }

    private static Process LaunchJar(
        Server server,
        ServerLaunchConfiguration configuration)
    {
        var jarPath = GetAbsolutePath(
            server.LocalPath,
            configuration.FilePath);

        if (!File.Exists(jarPath))
        {
            throw new FileNotFoundException(
                "Minecraft server JAR was not found.",
                jarPath);
        }

        var javaPath = string.IsNullOrWhiteSpace(configuration.JavaPath)
            ? "java"
            : configuration.JavaPath;

        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = server.LocalPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = false
        };

        AddArguments(
            startInfo,
            configuration.JvmArguments);

        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(jarPath);

        AddArguments(
            startInfo,
            configuration.ProgramArguments);

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Failed to start Minecraft server JAR.");
    }

    private static string GetAbsolutePath(
        string workingDirectory,
        string path)
    {
        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(
                Path.Combine(workingDirectory, path));
    }

    private static void AddArguments(
        ProcessStartInfo startInfo,
        string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return;
        }

        foreach (var argument in SplitArguments(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static IEnumerable<string> SplitArguments(
        string arguments)
    {
        var current = new List<char>();
        var inQuotes = false;

        foreach (var character in arguments)
        {
            if (character == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Count > 0)
                {
                    yield return new string(current.ToArray());
                    current.Clear();
                }

                continue;
            }

            current.Add(character);
        }

        if (inQuotes)
        {
            throw new ArgumentException(
                "Launch arguments contain an unmatched quote.",
                nameof(arguments));
        }

        if (current.Count > 0)
        {
            yield return new string(current.ToArray());
        }
    }
}