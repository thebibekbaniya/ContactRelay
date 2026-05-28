using ContactRelay.Models;

namespace ContactRelay.Data;

public interface ISyncRepository
{
    Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TargetMailbox>> GetEnabledTargetMailboxesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishedContactRecord>> GetPublishedContactOverridesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncExclusion>> GetEnabledExclusionsAsync(CancellationToken cancellationToken);

    Task<long> StartRunAsync(bool dryRun, CancellationToken cancellationToken);

    Task MarkStaleRunsAsFailedAsync(CancellationToken cancellationToken);

    Task CompleteRunAsync(long syncRunId, SyncRunSummary summary, CancellationToken cancellationToken);

    Task FailRunAsync(long syncRunId, string error, CancellationToken cancellationToken);

    Task UpsertPublishedContactsAsync(IEnumerable<DirectoryContact> contacts, CancellationToken cancellationToken);

    // Preload all sync states for all mailboxes in a single query
    Task<ILookup<string, ContactSyncState>> GetAllSyncStatesAsync(CancellationToken cancellationToken);

    // Preload cached folder IDs and last sync statuses for all mailboxes
    Task<IReadOnlyDictionary<string, MailboxFolderState>> GetMailboxFolderStatesAsync(CancellationToken cancellationToken);

    // Bulk-insert all run items for a mailbox in a single connection
    Task AddRunItemsBulkAsync(long syncRunId, IReadOnlyList<SyncRunItemDto> items, CancellationToken cancellationToken);

    // Upsert the TargetMailbox row once per mailbox and return the TargetMailboxId
    Task<long> EnsureTargetMailboxAsync(TargetMailbox mailbox, CancellationToken cancellationToken);

    // Upsert ContactSyncState rows for all created/updated contacts in a single connection
    Task BulkUpsertContactSyncStatesAsync(
        long targetMailboxId,
        string targetMailboxUpn,
        IReadOnlyList<ContactSyncStateUpdate> updates,
        CancellationToken cancellationToken);

    // Mark multiple sync state rows as deleted in a single connection
    Task BulkMarkSyncStatesDeletedAsync(
        string targetMailboxUpn,
        IReadOnlyList<Guid> sourceUserObjectIds,
        CancellationToken cancellationToken);

    Task UpsertMailboxFolderStateAsync(
        string targetMailboxUpn,
        string managedFolderName,
        string? managedFolderId,
        string syncStatus,
        string? errorMessage,
        CancellationToken cancellationToken);

    Task RecordLegacyFolderCleanupAsync(
        string targetMailboxUpn,
        string legacyFolderName,
        string? legacyFolderId,
        string cleanupStatus,
        CancellationToken cancellationToken);
}
