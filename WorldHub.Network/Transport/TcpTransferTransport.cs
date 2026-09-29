using WorldHub.Sync.Interfaces;

namespace WorldHub.Network.Transport;

public sealed class TcpTransferTransport : ITransferTransport
{
    private const int BufferSize = 64 * 1024;

    public async Task SendAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (totalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBytes),
                "Total bytes cannot be negative.");
        }

        var buffer = new byte[BufferSize];
        long transferredBytes = 0;

        while (true)
        {
            var bytesRead = await source.ReadAsync(
                buffer.AsMemory(0, buffer.Length),
                cancellationToken);

            if (bytesRead == 0)
            {
                break;
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);

            transferredBytes += bytesRead;

            progress?.Report(transferredBytes);
        }

        await destination.FlushAsync(cancellationToken);

        if (totalBytes > 0 && transferredBytes != totalBytes)
        {
            throw new IOException(
                $"Transfer size mismatch. " +
                $"Expected {totalBytes} bytes, received {transferredBytes} bytes.");
        }
    }

    public async Task ReceiveAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);

        if (totalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBytes),
                "Total bytes cannot be negative.");
        }

        var buffer = new byte[BufferSize];
        long transferredBytes = 0;

        while (transferredBytes < totalBytes)
        {
            var remainingBytes = totalBytes - transferredBytes;
            var bufferLength = (int)Math.Min(
                buffer.Length,
                remainingBytes);

            var bytesRead = await source.ReadAsync(
                buffer.AsMemory(0, bufferLength),
                cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    $"Transfer ended unexpectedly. " +
                    $"Expected {totalBytes} bytes, " +
                    $"received {transferredBytes} bytes.");
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);

            transferredBytes += bytesRead;
            progress?.Report(transferredBytes);
        }

        await destination.FlushAsync(
            cancellationToken);
    }
}