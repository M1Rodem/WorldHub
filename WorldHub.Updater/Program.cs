using WorldHub.Updater.Services;

namespace WorldHub.Updater;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            using var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "WorldHub-Updater/1.0");

            var releaseClient =
                new GitHubReleaseClient(httpClient);

            var commandRunner =
                new UpdaterCommandRunner(releaseClient);

            if (args.Length > 0 &&
                string.Equals(
                    args[0],
                    "--check",
                    StringComparison.OrdinalIgnoreCase))
            {
                var currentVersion =
                    GetRequiredArgument(args, "--version");

                return await commandRunner.CheckAsync(
                    currentVersion);
            }

            if (args.Length > 0 &&
                string.Equals(
                    args[0],
                    "--update",
                    StringComparison.OrdinalIgnoreCase))
            {
                var parser = new UpdateArgumentsParser();

                var arguments =
                    parser.Parse(args);


                var downloader =
                    new UpdateDownloader(
                        httpClient);


                var installer =
                    new UpdateInstaller();


                var updateRunner =
                    new UpdateRunner(
                        releaseClient,
                        downloader,
                        installer);


                await updateRunner.RunAsync(
                    arguments);

                return 0;
            }

            throw new ArgumentException(
                "Unknown updater command.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Updater failed: {exception.Message}");

            return 1;
        }
    }

    private static string GetRequiredArgument(
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