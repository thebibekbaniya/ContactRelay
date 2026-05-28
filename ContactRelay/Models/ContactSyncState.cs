namespace ContactRelay.Models;

public sealed class ContactSyncState
{
    public long ContactSyncStateId { get; init; }

    public long TargetMailboxId { get; init; }

    public required string TargetMailboxUpn { get; init; }

    public required Guid SourceUserObjectId { get; init; }

    public string? ExchangeContactId { get; init; }

    public string? LastFieldHash { get; init; }

    public bool IsDeleted { get; init; }

    public DateTimeOffset? LastSyncedUtc { get; init; }
}
