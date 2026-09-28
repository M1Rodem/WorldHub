namespace WorldHub.Sync.Interfaces;

public interface IWorldHashService
{
    Task<string> ComputeAsync(
        string worldPath,
        CancellationToken cancellationToken = default);
}