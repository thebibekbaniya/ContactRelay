namespace ContactRelay.Models;

public sealed class TargetMailbox
{
    public long TargetMailboxId { get; init; }

    public Guid? EntraUserObjectId { get; init; }

    public required string UserPrincipalName { get; init; }

    public required string Mail { get; init; }

    public string? DisplayName { get; init; }

    public bool IsEnabled { get; init; } = true;

    public string Source { get; init; } = "Sql";
}
