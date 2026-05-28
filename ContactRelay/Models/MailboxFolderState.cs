namespace ContactRelay.Models;

public sealed record MailboxFolderState(
    string? ManagedFolderId,
    string? LastSyncStatus
);
