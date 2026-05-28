namespace ContactRelay.Models;

public sealed record ManagedMailboxContact
{
    public required string ContactId { get; init; }

    public Guid? SourceUserObjectId { get; init; }

    public string? Email { get; init; }

    public string? FieldHash { get; init; }

    public string? ActualFieldHash { get; init; }

    public bool HasPersonalNotes { get; init; }

    public bool IsManaged { get; init; }
}
