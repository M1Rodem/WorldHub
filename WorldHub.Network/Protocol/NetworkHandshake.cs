using System.Text;
using WorldHub.Network.Models;

namespace WorldHub.Network.Protocol;

public static class NetworkHandshake
{
    private const string ClientHello = "WORLDHUB_HELLO";
    private const string ServerHello = "WORLDHUB_READY";

    public static async Task SendHelloAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await WriteMessageAsync(
            connection.Stream,
            ClientHello,
            cancellationToken);
    }

    public static async Task AcceptHelloAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var message = await ReadMessageAsync(
            connection.Stream,
            cancellationToken);

        if (!string.Equals(
                message,
                ClientHello,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unexpected handshake message: '{message}'.");
        }

        await WriteMessageAsync(
            connection.Stream,
            ServerHello,
            cancellationToken);
    }

    public static async Task WaitForServerHelloAsync(
        NetworkConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var message = await ReadMessageAsync(
            connection.Stream,
            cancellationToken);

        if (!string.Equals(
                message,
                ServerHello,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unexpected server handshake message: '{message}'.");
        }
    }

    private static async Task WriteMessageAsync(
        Stream stream,
        string message,
        CancellationToken cancellationToken)
    {
        var data = Encoding.UTF8.GetBytes(message);

        var length = BitConverter.GetBytes(data.Length);

        await stream.WriteAsync(
            length.AsMemory(),
            cancellationToken);

        await stream.WriteAsync(
            data.AsMemory(),
            cancellationToken);

        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<string> ReadMessageAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[sizeof(int)];

        await ReadExactlyAsync(
            stream,
            lengthBuffer,
            cancellationToken);

        var length = BitConverter.ToInt32(lengthBuffer);

        if (length <= 0 || length > 1024)
        {
            throw new InvalidDataException(
                $"Invalid message length: {length}.");
        }

        var buffer = new byte[length];

        await ReadExactlyAsync(
            stream,
            buffer,
            cancellationToken);

        return Encoding.UTF8.GetString(buffer);
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