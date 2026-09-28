using WorldHub.Core.Entities;

namespace WorldHub.Core.Interfaces;

public interface ITransferRepository
{
    Task<Transfer?> GetByIdAsync(
        Guid transferId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Transfer>> GetByWorldIdAsync(
        Guid worldId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Transfer transfer,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Transfer transfer,
        CancellationToken cancellationToken = default);
}