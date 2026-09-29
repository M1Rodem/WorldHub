namespace WorldHub.Sync.Interfaces;

public interface ITransferTransport
{
    Task SendAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);

    Task ReceiveAsync(
        Stream source,
        Stream destination,
        long totalBytes,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default);
}