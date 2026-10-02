using System.Security.Cryptography;
using System.Text;
using WorldHub.Sync.Interfaces;

namespace WorldHub.Sync.Hashing;

public sealed class WorldHashService : IWorldHashService
{
    private static readonly HashSet<string> IgnoredFiles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "session.lock"
        };

    public async Task<string> ComputeAsync(
        string worldPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldPath);

        if (!Directory.Exists(worldPath))
        {
            throw new DirectoryNotFoundException(
                $"Minecraft world directory was not found: {worldPath}");
        }

        var files = Directory
            .EnumerateFiles(
                worldPath,
                "*",
                SearchOption.AllDirectories)
            .Where(file =>
            {
                var relativePath =
                    Path.GetRelativePath(
                        worldPath,
                        file);

                return !IsIgnoredFile(relativePath);
            })
            .OrderBy(
                path =>
                    Path.GetRelativePath(
                        worldPath,
                        path),
                StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var hash =
            IncrementalHash.CreateHash(
                HashAlgorithmName.SHA256);

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path
                .GetRelativePath(
                    worldPath,
                    file)
                .Replace('\\', '/');

            var pathBytes =
                Encoding.UTF8.GetBytes(
                    relativePath);

            hash.AppendData(
                BitConverter.GetBytes(
                    pathBytes.Length));

            hash.AppendData(
                pathBytes);

            await using var stream =
                new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    bufferSize: 1024 * 64,
                    useAsync: true);

            var buffer =
                new byte[1024 * 64];

            int bytesRead;

            while ((bytesRead =
                       await stream.ReadAsync(
                           buffer,
                           cancellationToken)) > 0)
            {
                hash.AppendData(
                    buffer.AsSpan(
                        0,
                        bytesRead));
            }
        }

        return Convert.ToHexString(
                hash.GetHashAndReset())
            .ToLowerInvariant();
    }

    private static bool IsIgnoredFile(
        string relativePath)
    {
        var normalizedPath =
            relativePath.Replace(
                '\\',
                '/');

        var fileName =
            Path.GetFileName(
                normalizedPath);

        return IgnoredFiles.Contains(
            fileName);
    }
}