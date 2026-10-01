using WorldHub.Updater.Models;

namespace WorldHub.Updater.Services;

public sealed class UpdateArgumentsParser
{
    public UpdateArguments Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);


        var applicationPath =
            GetRequiredValue(
                args,
                "--app");


        var currentVersion =
            GetRequiredValue(
                args,
                "--version");


        var processIdText =
            GetRequiredValue(
                args,
                "--pid");


        if (!int.TryParse(
                processIdText,
                out var processId))
        {
            throw new ArgumentException(
                "Process ID must be a valid number.");
        }


        if (processId <= 0)
        {
            throw new ArgumentException(
                "Process ID must be greater than zero.");
        }


        return new UpdateArguments(
            applicationPath,
            currentVersion,
            processId);
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