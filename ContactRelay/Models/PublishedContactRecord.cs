namespace ContactRelay.Models;

public sealed class PublishedContactRecord
{
    public long PublishedContactId { get; init; }

    public required Guid SourceUserObjectId { get; init; }

    public string? UserPrincipalName { get; init; }

    public required string Mail { get; init; }

    public string? DisplayName { get; init; }

    public string? FieldHash { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool IsDeleted { get; init; }

    public bool IsManualOverride { get; init; }
}
