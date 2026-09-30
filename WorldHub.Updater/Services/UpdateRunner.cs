using WorldHub.Updater.Models;

namespace WorldHub.Updater.Services;

public sealed class UpdateRunner
{
    public Task RunAsync(
        UpdateArguments arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        cancellationToken.ThrowIfCancellationRequested();

        var applicationPath =
            Path.GetFullPath(arguments.ApplicationPath);

        var packagePath =
            Path.GetFullPath(arguments.PackagePath);

        if (!File.Exists(applicationPath))
        {
            throw new FileNotFoundException(
                "WorldHub application was not found.",
                applicationPath);
        }

        if (!File.Exists(packagePath))
        {
            throw new FileNotFoundException(
                "Update package was not found.",
                packagePath);
        }

        Console.WriteLine(
            $"Application: {applicationPath}");

        Console.WriteLine(
            $"Package: {packagePath}");

        Console.WriteLine(
            "Update arguments validated.");

        return Task.CompletedTask;
    }
}