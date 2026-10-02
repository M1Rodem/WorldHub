using WorldHub.Network.Models;
using WorldHub.Network.Protocol;

namespace WorldHub.Network.Transfer;

/// <summary>
/// Общий приём файлов снапшота с прогрессом и валидацией.
/// </summary>
public static class SnapshotFileReceiver
{
    private const int BufferSize = 64 * 1024;

    public static async Task<long> ReceiveFilesAsync(
        NetworkConnection connection,
        string targetDirectory,
        int fileCount,
        long expectedTotalBytes,
        IProgress<long>? progress,
        long alreadyTransferred,
        CancellationToken cancellationToken)
    {
        long receivedBytes = 0;

        for (var index = 0; index < fileCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileMetadata = await WorldTransferProtocol
                .ReceiveJsonAsync<SnapshotFileMetadata>(connection, cancellationToken);

            var relativePath = TransferPathValidator
                .ValidateRelativePath(fileMetadata.RelativePath);

            if (fileMetadata.Length < 0)
            {
                throw new InvalidDataException(
                    "Received file has an invalid length.");
            }

            var targetFile = Path.Combine(targetDirectory, relativePath);
            var targetDir = Path.GetDirectoryName(targetFile);

            if (string.IsNullOrWhiteSpace(targetDir))
            {
                throw new InvalidDataException("Invalid received file path.");
            }

            Directory.CreateDirectory(targetDir);

            await using var destination = new FileStream(
                targetFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            if (progress is not null)
            {
                var fileProgress = new Progress<long>(
                    fileTransferred =>
                        progress.Report(alreadyTransferred + receivedBytes + fileTransferred));

                await WorldTransferProtocol.ReceiveFileAsync(
                    connection,
                    destination,
                    fileMetadata.Length,
                    fileProgress,
                    cancellationToken);
            }
            else
            {
                await WorldTransferProtocol.ReceiveFileAsync(
                    connection,
                    destination,
                    fileMetadata.Length,
                    cancellationToken: cancellationToken);
            }

            receivedBytes += fileMetadata.Length;
        }

        if (receivedBytes != expectedTotalBytes)
        {
            throw new InvalidDataException(
                $"Transfer size mismatch. " +
                $"Expected {expectedTotalBytes} bytes, " +
                $"received {receivedBytes} bytes.");
        }

        return receivedBytes;
    }
}