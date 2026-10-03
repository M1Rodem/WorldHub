using System.Text;
using System.Text.Json;
using WorldHub.Network.Models;

namespace WorldHub.Network.Protocol;

public static class WorldTransferProtocol
{
    private const int MaxMessageSize = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public const string PushRequest = "WORLDHUB_PUSH";
    public const string PullRequest = "WORLDHUB_PULL";

    public const string SnapshotMetadata = "WORLDHUB_SNAPSHOT";
    public const string FileHeader = "WORLDHUB_FILE";
    public const string TransferCompleted = "WORLDHUB_TRANSFER_COMPLETED";
    public const string TransferApproved = "WORLDHUB_TRANSFER_APPROVED";
    public const string TransferAccepted = "WORLDHUB_TRANSFER_ACCEPTED";
    public const string TransferRejected = "WORLDHUB_TRANSFER_REJECTED";
    public const string TransferError = "WORLDHUB_TRANSFER_ERROR";

    public static async Task SendMessageAsync(
        NetworkConnection connection,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var data = Encoding.UTF8.GetBytes(message);

        await WriteFrameAsync(
            connection.Stream,
            data,
            cancellationToken);
    }

    public static async Task<string> ReceiveMessageAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var data = await ReadFrameAsync(
            connection.Stream,
            cancellationToken);

        return Encoding.UTF8.GetString(data);
    }

    public static async Task SendJsonAsync<T>(
        NetworkConnection connection,
        T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var json = JsonSerializer.Serialize(
            value,
            JsonOptions);

        await SendMessageAsync(
            connection,
            json,
            cancellationToken);
    }

    public static async Task<T> ReceiveJsonAsync<T>(
        NetworkConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var json = await ReceiveMessageAsync(
            connection,
            cancellationToken);

        var value = JsonSerializer.Deserialize<T>(
            json,
            JsonOptions);

        return value
            ?? throw new InvalidDataException(
                $"Unable to deserialize transfer message as {typeof(T).Name}.");
    }

    public static async Task SendFileAsync(
        NetworkConnection connection,
        Stream source,
        long totalBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(source);

        if (totalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBytes));
        }

        var buffer = new byte[64 * 1024];
        long transferred = 0;

        while (transferred < totalBytes)
        {
            var remaining = totalBytes - transferred;

            var bytesToRead = (int)Math.Min(
                buffer.Length,
                remaining);

            var bytesRead = await source.ReadAsync(
                buffer.AsMemory(0, bytesToRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    "Source stream ended before the expected file size.");
            }

            await connection.Stream.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);

            transferred += bytesRead;
            progress?.Report(transferred);
        }
    }

    public static async Task ReceiveFileAsync(
        NetworkConnection connection,
        Stream destination,
        long totalBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(destination);

        if (totalBytes < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalBytes));
        }

        var buffer = new byte[64 * 1024];
        long transferred = 0;

        while (transferred < totalBytes)
        {
            var remaining = totalBytes - transferred;

            var bytesToRead = (int)Math.Min(
                buffer.Length,
                remaining);

            var bytesRead = await connection.Stream.ReadAsync(
                buffer.AsMemory(0, bytesToRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    "Remote side closed the connection during file transfer.");
            }

            await destination.WriteAsync(
                buffer.AsMemory(0, bytesRead),
                cancellationToken);

            transferred += bytesRead;
            progress?.Report(transferred);
        }

        await destination.FlushAsync(cancellationToken);
    }

    private static async Task WriteFrameAsync(
        Stream stream,
        byte[] data,
        CancellationToken cancellationToken)
    {
        if (data.Length <= 0 || data.Length > MaxMessageSize)
        {
            throw new InvalidDataException(
                $"Invalid transfer message size: {data.Length}.");
        }

        var length = BitConverter.GetBytes(data.Length);

        await stream.WriteAsync(
            length.AsMemory(),
            cancellationToken);

        await stream.WriteAsync(
            data.AsMemory(),
            cancellationToken);

        await stream.FlushAsync(
            cancellationToken);
    }

    private static async Task<byte[]> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[sizeof(int)];

        await ReadExactlyAsync(
            stream,
            lengthBuffer,
            cancellationToken);

        var length = BitConverter.ToInt32(lengthBuffer);

        if (length <= 0 || length > MaxMessageSize)
        {
            throw new InvalidDataException(
                $"Invalid transfer message length: {length}.");
        }

        var buffer = new byte[length];

        await ReadExactlyAsync(
            stream,
            buffer,
            cancellationToken);

        return buffer;
    }

    private static async Task ReadExactlyAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(
                    offset,
                    buffer.Length - offset),
                cancellationToken);

            if (bytesRead == 0)
            {
                throw new EndOfStreamException(
                    "The remote side closed the connection.");
            }

            offset += bytesRead;
        }
    }
}