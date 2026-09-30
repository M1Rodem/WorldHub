using WorldHub.Updater.Models;

namespace WorldHub.Updater.Services;

public sealed class UpdateArgumentsParser
{
    public UpdateArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var applicationPath = GetRequiredValue(
            args,
            "--app");

        var packagePath = GetRequiredValue(
            args,
            "--package");

        return new UpdateArguments(
            applicationPath,
            packagePath);
    }

    private static string GetRequiredValue(
        IReadOnlyList<string> args,
        string argumentName)
    {
        for (var index = 0; index < args.Count - 1; index++)
        {
            if (!string.Equals(
                    args[index],
                    argumentName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = args[index + 1];

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new ArgumentException(
            $"Required argument '{argumentName}' was not provided.");
    }
}