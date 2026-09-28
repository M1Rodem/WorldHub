using WorldHub.Core.Enums;

namespace WorldHub.Core.Entities;

public sealed class Transfer
{
    public Guid Id { get; init; }

    public Guid WorldId { get; init; }

    public long SnapshotId { get; init; }

    public Guid SenderId { get; init; }

    public Guid ReceiverId { get; init; }

    public TransferDirection Direction { get; init; }

    public TransferStatus Status { get; set; }

    public long TotalBytes { get; set; }

    public long TransferredBytes { get; set; }

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; set; }

    public string? Error { get; set; }
}