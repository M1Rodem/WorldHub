namespace WorldHub.Updater.Services;

public sealed class UpdateDownloader
{
    private readonly HttpClient _httpClient;

    public UpdateDownloader(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
    }


    public async Task<string> DownloadAsync(
        string downloadUrl,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            downloadUrl);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            destinationPath);


        var directory =
            Path.GetDirectoryName(destinationPath);


        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        using var response =
            await _httpClient.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);


        response.EnsureSuccessStatusCode();


        await using var source =
            await response.Content.ReadAsStreamAsync(
                cancellationToken);


        await using var destination =
            File.Create(destinationPath);


        await source.CopyToAsync(
            destination,
            cancellationToken);


        return destinationPath;
    }
}