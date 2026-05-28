namespace ContactRelay.Models;

public sealed record SyncRunItemDto(
    string? TargetMailboxUpn,
    Guid? SourceUserObjectId,
    SyncAction Action,
    SyncActionResult Result,
    string Message,
    string? GraphContactId,
    string? ErrorCode
);
