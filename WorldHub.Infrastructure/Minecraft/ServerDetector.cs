using System.Text.RegularExpressions;
using WorldHub.Core.Entities;
using WorldHub.Core.Enums;
using WorldHub.Core.Interfaces;
using WorldHub.Logging;

namespace WorldHub.Infrastructure.Minecraft;

public sealed class ServerDetector : IServerDetector
{
    public ServerDetectionResult Detect(string launchFilePath)
    {
        if (string.IsNullOrWhiteSpace(launchFilePath))
        {
            throw new ArgumentException(
                "Launch file path cannot be empty.",
                nameof(launchFilePath));
        }

        var fullLaunchFilePath =
            Path.GetFullPath(launchFilePath);

        if (!File.Exists(fullLaunchFilePath))
        {
            throw new FileNotFoundException(
                "Launch file was not found.",
                fullLaunchFilePath);
        }

        var serverDirectory =
            Path.GetDirectoryName(fullLaunchFilePath);

        if (string.IsNullOrWhiteSpace(serverDirectory))
        {
            throw new InvalidOperationException(
                "Server directory could not be determined.");
        }

        var launchType =
            DetectLaunchType(fullLaunchFilePath);

        var metadata =
            DetectServerMetadata(serverDirectory);

        AppLog.Log(
            $"[DETECTOR] Detected {metadata.Loader} (MC: {metadata.MinecraftVersion}, Type: {launchType}) at {serverDirectory}");

        return new ServerDetectionResult
        {
            ServerDirectory = serverDirectory,
            MinecraftVersion = metadata.MinecraftVersion,
            Loader = metadata.Loader,
            LoaderVersion = metadata.LoaderVersion,

            LaunchConfiguration = new ServerLaunchConfiguration
            {
                Type = launchType,
                FilePath = Path.GetRelativePath(
                    serverDirectory,
                    fullLaunchFilePath)
            }
        };
    }

    private static ServerLaunchType DetectLaunchType(
        string launchFilePath)
    {
        var extension =
            Path.GetExtension(launchFilePath);

        if (extension.Equals(
                ".bat",
                StringComparison.OrdinalIgnoreCase)
            || extension.Equals(
                ".cmd",
                StringComparison.OrdinalIgnoreCase))
        {
            return ServerLaunchType.Script;
        }

        if (extension.Equals(
                ".jar",
                StringComparison.OrdinalIgnoreCase))
        {
            return ServerLaunchType.Jar;
        }

        throw new InvalidOperationException(
            "Неподдерживаемый файл запуска. " +
            "Выберите .bat, .cmd или .jar.");
    }

    private static (
    string MinecraftVersion,
    string Loader,
    string? LoaderVersion)
    DetectServerMetadata(string serverDirectory)
    {
        // 1. Проверяем специализированные файлы конфигурации автоустановщиков (например, forge-auto-install.txt)
        var autoInstallConfig = Path.Combine(serverDirectory, "forge-auto-install.txt");
        if (File.Exists(autoInstallConfig))
        {
            var detected = TryParseAutoInstallConfig(autoInstallConfig);
            if (detected is not null)
            {
                return detected.Value;
            }
        }

        // 2. Сканируем файлы сервера
        var files =
            Directory.EnumerateFiles(
                    serverDirectory,
                    "*",
                    SearchOption.AllDirectories)
                .Select(path =>
                    Path.GetRelativePath(
                        serverDirectory,
                        path))
                .ToArray();

        // Forge:
        // libraries/net/minecraftforge/forge/1.20.1-47.4.13/...
        var forgeMatch = files
            .Select(path => path.Replace('\\', '/'))
            .Select(path => Regex.Match(
                path,
                @"libraries/net/minecraftforge/forge/" +
                @"(?<minecraft>\d+\.\d+(?:\.\d+)?)-" +
                @"(?<forge>\d+\.\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase))
            .FirstOrDefault(match => match.Success);

        if (forgeMatch is not null)
        {
            return (
                forgeMatch.Groups["minecraft"].Value,
                "Forge",
                forgeMatch.Groups["forge"].Value);
        }

        // NeoForge:
        // libraries/net/neoforged/neoforge/20.4.164-beta/...
        var neoForgeMatch = files
            .Select(path => path.Replace('\\', '/'))
            .Select(path => Regex.Match(
                path,
                @"libraries/net/neoforged/neoforge/" +
                @"(?<version>\d+\.\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase))
            .FirstOrDefault(match => match.Success);

        if (neoForgeMatch is not null)
        {
            return (
                DetectMinecraftVersion(files, serverDirectory),
                "NeoForge",
                neoForgeMatch.Groups["version"].Value);
        }

        // Fabric:
        // libraries/net/fabricmc/fabric-loader/0.15.11/...
        var fabricMatch = files
            .Select(path => path.Replace('\\', '/'))
            .Select(path => Regex.Match(
                path,
                @"libraries/net/fabricmc/fabric-loader/" +
                @"(?<version>\d+\.\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase))
            .FirstOrDefault(match => match.Success);

        if (fabricMatch is not null)
        {
            return (
                DetectMinecraftVersion(files, serverDirectory),
                "Fabric",
                fabricMatch.Groups["version"].Value);
        }

        // Quilt:
        // libraries/org/quiltmc/quilt-loader/...
        var quiltMatch = files
            .Select(path => path.Replace('\\', '/'))
            .Select(path => Regex.Match(
                path,
                @"libraries/org/quiltmc/quilt-loader/" +
                @"(?<version>\d+\.\d+(?:\.\d+)?)",
                RegexOptions.IgnoreCase))
            .FirstOrDefault(match => match.Success);

        if (quiltMatch is not null)
        {
            return (
                DetectMinecraftVersion(files, serverDirectory),
                "Quilt",
                quiltMatch.Groups["version"].Value);
        }

        // 3. Проверка явных признаков Forge/Fabric/NeoForge в корневых конфигурациях и скриптах
        var loaderFromEnv = TryDetectLoaderFromEnvironment(serverDirectory, files);
        if (loaderFromEnv is not null)
        {
            return (
                DetectMinecraftVersion(files, serverDirectory),
                loaderFromEnv.Value.Loader,
                loaderFromEnv.Value.LoaderVersion);
        }

        // 4. Vanilla (чистый сервер)
        return (
            DetectMinecraftVersion(files, serverDirectory),
            "Vanilla",
            null);
    }

    private static (string MinecraftVersion, string Loader, string? LoaderVersion)?
        TryParseAutoInstallConfig(string filePath)
    {
        try
        {
            string? mcVersion = null;
            string? loader = null;
            string? loaderVersion = null;

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#') || !trimmed.Contains('='))
                {
                    continue;
                }

                var parts = trimmed.Split('=', 2);
                var key = parts[0].Trim().ToLowerInvariant();
                var val = parts[1].Trim();

                if (key == "minecraftversion")
                {
                    mcVersion = val;
                }
                else if (key == "loadertype")
                {
                    loader = val switch
                    {
                        var s when s.Equals("forge", StringComparison.OrdinalIgnoreCase) => "Forge",
                        var s when s.Equals("neoforge", StringComparison.OrdinalIgnoreCase) => "NeoForge",
                        var s when s.Equals("fabric", StringComparison.OrdinalIgnoreCase) => "Fabric",
                        var s when s.Equals("quilt", StringComparison.OrdinalIgnoreCase) => "Quilt",
                        _ => char.ToUpperInvariant(val[0]) + val[1..].ToLowerInvariant()
                    };
                }
                else if (key == "loaderversion")
                {
                    loaderVersion = val;
                }
            }

            if (!string.IsNullOrWhiteSpace(mcVersion) && !string.IsNullOrWhiteSpace(loader))
            {
                return (mcVersion, loader, string.IsNullOrWhiteSpace(loaderVersion) ? null : loaderVersion);
            }
        }
        catch
        {
        }

        return null;
    }

    private static (string Loader, string? LoaderVersion)?
        TryDetectLoaderFromEnvironment(string serverDirectory, IEnumerable<string> files)
    {
        // Проверяем наличие server_starter.conf или файлов с упоминанием forge / fabric
        var serverStarterConf = Path.Combine(serverDirectory, "server_starter.conf");
        if (File.Exists(serverStarterConf))
        {
            try
            {
                var text = File.ReadAllText(serverStarterConf);
                if (text.Contains("Forge", StringComparison.OrdinalIgnoreCase))
                {
                    return ("Forge", null);
                }
            }
            catch
            {
            }
        }

        // Проверяем bat/sh скрипты запуска на упоминание forge, neoforge, fabric
        foreach (var scriptName in new[] { "start_server.bat", "start.bat", "run.bat", "start_server.sh", "run.sh" })
        {
            var scriptPath = Path.Combine(serverDirectory, scriptName);
            if (File.Exists(scriptPath))
            {
                try
                {
                    var text = File.ReadAllText(scriptPath);
                    if (Regex.IsMatch(text, @"\bforge\b", RegexOptions.IgnoreCase))
                    {
                        return ("Forge", null);
                    }
                    if (Regex.IsMatch(text, @"\bneoforge\b", RegexOptions.IgnoreCase))
                    {
                        return ("NeoForge", null);
                    }
                    if (Regex.IsMatch(text, @"\bfabric\b", RegexOptions.IgnoreCase))
                    {
                        return ("Fabric", null);
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    private static string DetectMinecraftVersion(
        IEnumerable<string> files,
        string serverDirectory)
    {
        // 1. Проверяем forge-auto-install.txt в первую очередь
        var autoInstallConfig = Path.Combine(serverDirectory, "forge-auto-install.txt");
        if (File.Exists(autoInstallConfig))
        {
            var parsed = TryParseAutoInstallConfig(autoInstallConfig);
            if (parsed is not null && !string.IsNullOrWhiteSpace(parsed.Value.MinecraftVersion))
            {
                return parsed.Value.MinecraftVersion;
            }
        }

        // 2. Ищем версию в файлах и путях
        foreach (var file in files)
        {
            var match = Regex.Match(
                file,
                @"(?<!\d)(1\.\d+(?:\.\d+)?)(?!\d)");

            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        throw new InvalidOperationException(
            "Не удалось определить версию Minecraft.");
    }
}