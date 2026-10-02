using System.Reflection;

namespace WorldHub.App.Services.Update;

public sealed class AppVersionService
{
    public string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informationalVersion =
            assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return NormalizeVersion(informationalVersion);
        }

        var version = assembly.GetName().Version;

        if (version is null)
        {
            throw new InvalidOperationException(
                "Application version could not be determined.");
        }

        return NormalizeVersion(version.ToString());
    }

    private static string NormalizeVersion(string version)
    {
        var normalized = version.Trim();

        var plusIndex = normalized.IndexOf('+');

        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        if (normalized.StartsWith(
                "v",
                StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }

        return normalized;
    }
}