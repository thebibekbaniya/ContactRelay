using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using ContactRelay.Data;
using ContactRelay.Graph;
using ContactRelay.Mapping;
using ContactRelay.Models;
using ContactRelay.Options;
using ContactRelay.Utilities;

namespace ContactRelay.Services;

public sealed class SyncOrchestrator(
    ISyncRepository repository,
    IDirectoryGraphService directoryGraphService,
    IMailboxContactGraphService mailboxContactGraphService,
    IContactMapper mapper,
    IOptions<SyncWorkerOptions> syncOptions,
    ILogger<SyncOrchestrator> logger) : ISyncOrchestrator
{
    private readonly SyncWorkerOptions _syncOptions = syncOptions.Value;

    // Carries per-mailbox counts back to the parallel aggregator.
    private readonly record struct MailboxSyncCounts(int Created, int Updated, int Deleted, int Skipped, int Errors);

    public async Task<SyncRunSummary> RunAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        directoryGraphService.ClearRunCache();

        var sqlSettings = await repository.GetSettingsAsync(cancellationToken);
        var dryRun = GetBool(sqlSettings, "DryRun") ?? _syncOptions.DryRun;
        var deleteOutOfScope =
            GetBool(sqlSettings, "DeleteOutOfScopeContacts") ?? _syncOptions.DeleteOutOfScopeContacts;
        var targetAllUserMailboxes =
            GetBool(sqlSettings, "TargetAllUserMailboxes") ?? _syncOptions.TargetAllUserMailboxes;
        var managedFolderName = GetString(sqlSettings, "ManagedContactFolderName")
                                ?? GetString(sqlSettings, "ManagedFolderName")
                                ?? _syncOptions.ManagedFolderName;
        var managedCategory = GetString(sqlSettings, "ManagedCategory") ?? _syncOptions.ManagedCategory;
        var contactMappingOptions = CreateContactMappingOptions(managedCategory);
        var deleteLegacyFolders = GetBool(sqlSettings, "DeleteLegacyFolderAfterSuccessfulSync")
                                  ?? GetBool(sqlSettings, "DeleteLegacyFoldersAfterSuccessfulSync")
                                  ?? (_syncOptions.DeleteLegacyFolderAfterSuccessfulSync &&
                                      _syncOptions.DeleteLegacyFoldersAfterSuccessfulSync);
        var legacyFolderNames = GetLegacyFolderNames(sqlSettings);

        await repository.MarkStaleRunsAsFailedAsync(cancellationToken);
        var syncRunId = await repository.StartRunAsync(dryRun, cancellationToken);
        var summary = new SyncRunSummary { SyncRunId = syncRunId };

        logger.LogInformation(
            "Starting directory contact sync run {SyncRunId}. DryRun={DryRun} ManagedFolder={ManagedFolder} TargetAllUserMailboxes={TargetAllUserMailboxes} Concurrency={Concurrency}",
            syncRunId, dryRun, managedFolderName, targetAllUserMailboxes, _syncOptions.MailboxConcurrency);

        try
        {
            var exclusions = await repository.GetEnabledExclusionsAsync(cancellationToken);
            var targetMailboxes = await LoadTargetMailboxesAsync(exclusions, targetAllUserMailboxes, cancellationToken);
            var publishedOverrides = await repository.GetPublishedContactOverridesAsync(cancellationToken);
            var includedSourceIds = publishedOverrides.Select(c => c.SourceUserObjectId).ToHashSet();
            var publishedContacts =
                await directoryGraphService.GetPublishedContactsAsync(includedSourceIds, cancellationToken);
            publishedContacts = ApplySourceExclusions(publishedContacts, exclusions);
            publishedContacts = publishedContacts.Where(HasAtLeastOnePhoneNumber).ToArray();

            await repository.UpsertPublishedContactsAsync(publishedContacts, cancellationToken);

            summary.TargetMailboxCount = targetMailboxes.Count;
            summary.PublishedContactCount = publishedContacts.Count;

            var contactsBySourceId = publishedContacts.ToDictionary(c => c.SourceUserObjectId);

            // Preload state data so the mailbox loop does not repeat the same SQL queries.
            var allSyncStates = await repository.GetAllSyncStatesAsync(cancellationToken);
            var folderStates = await repository.GetMailboxFolderStatesAsync(cancellationToken);

            var mailboxResults = new ConcurrentBag<MailboxSyncCounts>();
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _syncOptions.MailboxConcurrency,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(targetMailboxes, parallelOptions, async (mailbox, ct) =>
            {
                var mailboxStates = allSyncStates[mailbox.UserPrincipalName].ToList();
                folderStates.TryGetValue(mailbox.UserPrincipalName, out var folderState);

                var counts = await SyncMailboxAsync(
                    syncRunId, mailbox, contactsBySourceId,
                    mailboxStates, folderState,
                    managedFolderName, managedCategory, contactMappingOptions,
                    deleteOutOfScope, deleteLegacyFolders, legacyFolderNames,
                    dryRun, ct);

                mailboxResults.Add(counts);
            });

            summary.CreatedCount = mailboxResults.Sum(r => r.Created);
            summary.UpdatedCount = mailboxResults.Sum(r => r.Updated);
            summary.DeletedCount = mailboxResults.Sum(r => r.Deleted);
            summary.SkippedCount = mailboxResults.Sum(r => r.Skipped);
            summary.ErrorCount = mailboxResults.Sum(r => r.Errors);

            await repository.CompleteRunAsync(syncRunId, summary, cancellationToken);
            logger.LogInformation(
                "Completed sync run {SyncRunId}. Targets={Targets} Published={Published} Created={Created} Updated={Updated} Deleted={Deleted} Skipped={Skipped} Errors={Errors} Elapsed={Elapsed}",
                syncRunId, summary.TargetMailboxCount, summary.PublishedContactCount,
                summary.CreatedCount, summary.UpdatedCount, summary.DeletedCount,
                summary.SkippedCount, summary.ErrorCount, stopwatch.Elapsed);

            return summary;
        }
        catch (Exception ex)
        {
            var graphError = GraphExceptionDetails.From(ex);
            await repository.FailRunAsync(syncRunId, $"ErrorType={ex.GetType().Name}; {graphError.ToSummary()}", CancellationToken.None);
            logger.LogError(
                "Sync run {SyncRunId} failed. ErrorType={ErrorType} GraphError={GraphError} Elapsed={Elapsed}",
                syncRunId,
                ex.GetType().Name,
                graphError.ToSummary(),
                stopwatch.Elapsed);
            throw;
        }
    }

    private async Task<IReadOnlyList<TargetMailbox>> LoadTargetMailboxesAsync(
        IReadOnlyList<SyncExclusion> exclusions,
        bool targetAllUserMailboxes,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TargetMailbox> targets;
        if (targetAllUserMailboxes)
        {
            targets = await directoryGraphService.GetAllUserMailboxesAsync(cancellationToken);
        }
        else
        {
            var sqlTargets = await repository.GetEnabledTargetMailboxesAsync(cancellationToken);
            var groupTargets = await directoryGraphService.GetTargetMailboxesFromGroupAsync(cancellationToken);
            targets = sqlTargets.Concat(groupTargets).ToArray();
        }

        return targets
            .GroupBy(mailbox => mailbox.Mail, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(mailbox => !IsTargetExcluded(mailbox, exclusions))
            .OrderBy(mailbox => mailbox.Mail, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<MailboxSyncCounts> SyncMailboxAsync(
        long syncRunId,
        TargetMailbox mailbox,
        IReadOnlyDictionary<Guid, DirectoryContact> publishedContacts,
        IReadOnlyList<ContactSyncState> mailboxStates,
        MailboxFolderState? folderState,
        string managedFolderName,
        string managedCategory,
        SyncWorkerOptions contactMappingOptions,
        bool deleteOutOfScope,
        bool deleteLegacyFolders,
        IReadOnlyList<string> legacyFolderNames,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        string? folderId = null;
        var runItems = new List<SyncRunItemDto>();

        try
        {
            var statesBySourceId = mailboxStates.ToDictionary(s => s.SourceUserObjectId);

            logger.LogInformation(
                "Synchronizing mailbox {Mailbox}.",
                LogRedactor.Identifier(mailbox.UserPrincipalName, _syncOptions));

            // Resolve folder ID.
            // Re-use the cached folder ID from the last successful run to skip
            // GetContactFoldersAsync on every repeat run.
            IReadOnlyList<Microsoft.Graph.Models.ContactFolder> allFolders = [];

            if (!string.IsNullOrWhiteSpace(folderState?.ManagedFolderId) && !dryRun)
            {
                folderId = folderState.ManagedFolderId;
                // Only pay for the folder list when we actually need it for legacy cleanup.
                if (deleteLegacyFolders)
                    allFolders =
                        await mailboxContactGraphService.GetContactFoldersAsync(mailbox.UserPrincipalName,
                            cancellationToken);
            }
            else
            {
                allFolders =
                    await mailboxContactGraphService.GetContactFoldersAsync(mailbox.UserPrincipalName,
                        cancellationToken);
                folderId = dryRun
                    ? allFolders.FirstOrDefault(f =>
                        string.Equals(f.DisplayName, managedFolderName, StringComparison.OrdinalIgnoreCase))?.Id
                    : await mailboxContactGraphService.EnsureManagedFolderAsync(mailbox.UserPrincipalName,
                        managedFolderName, allFolders, cancellationToken);
            }

            await repository.UpsertMailboxFolderStateAsync(
                mailbox.UserPrincipalName, managedFolderName, folderId, "Running", null, cancellationToken);

            // Fetch current managed contacts.
            var managedContacts = string.IsNullOrWhiteSpace(folderId)
                ? (IReadOnlyList<ManagedMailboxContact>)[]
                : await mailboxContactGraphService.GetManagedContactsAsync(
                    mailbox.UserPrincipalName, folderId, managedCategory, cancellationToken);

            managedContacts = ApplySyncStateMetadata(managedContacts, mailboxStates);

            var contactsBySourceId = managedContacts
                .Where(c => c.SourceUserObjectId.HasValue)
                .GroupBy(c => c.SourceUserObjectId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Decision phase.
            // Determine what to do for every published contact without any I/O.
            var toCreate = new List<(Guid SourceId, Microsoft.Graph.Models.Contact GraphContact)>();
            var toUpdate = new List<(Guid SourceId, string ContactId, Microsoft.Graph.Models.Contact GraphContact)>();
            var toDeleteDuplicates = new List<(ManagedMailboxContact Contact, Guid? SourceId)>();

            foreach (var source in publishedContacts.Values)
            {
                var existingList = contactsBySourceId.TryGetValue(source.SourceUserObjectId, out var lst)
                    ? lst
                    : (IReadOnlyList<ManagedMailboxContact>)[];
                var primary = existingList.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.ContactId));
                var state = statesBySourceId.TryGetValue(source.SourceUserObjectId, out var st) ? st : null;

                foreach (var dup in existingList.Skip(1))
                    toDeleteDuplicates.Add((dup, source.SourceUserObjectId));

                var graphContact = mapper.ToGraphContact(source, contactMappingOptions);

                if (primary is null)
                {
                    toCreate.Add((source.SourceUserObjectId, graphContact));
                }
                else
                {
                    var isChanged = !string.Equals(primary.ActualFieldHash, source.FieldHash,
                                        StringComparison.OrdinalIgnoreCase)
                                    || !string.Equals(state?.LastFieldHash, source.FieldHash,
                                        StringComparison.OrdinalIgnoreCase)
                                    || primary.HasPersonalNotes;

                    if (!isChanged)
                        runItems.Add(new SyncRunItemDto(
                            mailbox.UserPrincipalName, source.SourceUserObjectId,
                            SyncAction.Skip, SyncActionResult.Skipped,
                            "Managed contact is already current.", primary.ContactId, null));
                    else
                        toUpdate.Add((source.SourceUserObjectId, primary.ContactId, graphContact));
                }
            }

            // Execution phase.
            var syncStateUpdates = new List<ContactSyncStateUpdate>();
            var syncStateDeletes = new List<Guid>();
            int created = 0, updated = 0, deleted = 0;
            var skipped = runItems.Count; // already-decided skips
            var mailboxErrors = 0;

            if (dryRun)
            {
                foreach (var (sourceId, _) in toCreate)
                    runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Create,
                        SyncActionResult.Planned, "Dry-run planned managed contact create.", null, null));
                foreach (var (sourceId, contactId, _) in toUpdate)
                    runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Update,
                        SyncActionResult.Planned, "Dry-run planned managed contact update.", contactId, null));
                foreach (var (contact, sourceId) in toDeleteDuplicates)
                    runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Delete,
                        SyncActionResult.Planned, "Dry-run planned duplicate managed contact delete.",
                        contact.ContactId, null));

                created = toCreate.Count;
                updated = toUpdate.Count;
                deleted = toDeleteDuplicates.Count;
            }
            else
            {
                // Batch-delete duplicates
                if (toDeleteDuplicates.Count > 0)
                {
                    var dupIds = toDeleteDuplicates.Select(d => d.Contact.ContactId).ToList();
                    var failedDups =
                        await mailboxContactGraphService.BatchDeleteContactsAsync(mailbox.UserPrincipalName, folderId!,
                            dupIds, cancellationToken);
                    foreach (var (contact, sourceId) in toDeleteDuplicates)
                        if (!failedDups.Contains(contact.ContactId))
                        {
                            deleted++;
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Delete,
                                SyncActionResult.Success, "Duplicate managed contact removed.", contact.ContactId,
                                null));
                        }
                        else
                        {
                            mailboxErrors++;
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Error,
                                SyncActionResult.Failed, "Duplicate managed contact removal failed.", contact.ContactId,
                                "BatchDeleteFailed"));
                        }
                }

                // Batch-create new contacts
                if (toCreate.Count > 0)
                {
                    var createResults = await mailboxContactGraphService.BatchCreateContactsAsync(
                        mailbox.UserPrincipalName, folderId!, toCreate, cancellationToken);

                    foreach (var (sourceId, _) in toCreate)
                    {
                        var contactId = createResults.TryGetValue(sourceId, out var cid) ? cid : null;
                        if (!string.IsNullOrWhiteSpace(contactId))
                        {
                            created++;
                            syncStateUpdates.Add(new ContactSyncStateUpdate(sourceId, contactId,
                                publishedContacts[sourceId].FieldHash));
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Create,
                                SyncActionResult.Success, "Managed contact created.", contactId, null));
                        }
                        else
                        {
                            mailboxErrors++;
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Error,
                                SyncActionResult.Failed, "Managed contact creation failed.", null, "CreateFailed"));
                        }
                    }
                }

                // Batch-update changed contacts
                if (toUpdate.Count > 0)
                {
                    var failedUpdates = await mailboxContactGraphService.BatchUpdateContactsAsync(
                        mailbox.UserPrincipalName, folderId!, toUpdate, cancellationToken);

                    foreach (var (sourceId, contactId, _) in toUpdate)
                        if (!failedUpdates.Contains(sourceId))
                        {
                            updated++;
                            syncStateUpdates.Add(new ContactSyncStateUpdate(sourceId, contactId,
                                publishedContacts[sourceId].FieldHash));
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Update,
                                SyncActionResult.Success, "Managed contact updated.", contactId, null));
                        }
                        else
                        {
                            mailboxErrors++;
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, sourceId, SyncAction.Error,
                                SyncActionResult.Failed, "Managed contact update failed.", contactId, "UpdateFailed"));
                        }
                }
            }

            // Delete out-of-scope contacts.
            if (deleteOutOfScope && mailboxErrors == 0)
            {
                var inScopeIds = publishedContacts.Keys.ToHashSet();
                var toDeleteOos = managedContacts
                    .Where(c => c.SourceUserObjectId.HasValue && !inScopeIds.Contains(c.SourceUserObjectId!.Value))
                    .ToList();

                if (dryRun)
                {
                    foreach (var c in toDeleteOos)
                        runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, c.SourceUserObjectId,
                            SyncAction.Delete, SyncActionResult.Planned,
                            "Dry-run planned out-of-scope managed contact delete.", c.ContactId, null));
                    deleted += toDeleteOos.Count;
                }
                else if (toDeleteOos.Count > 0)
                {
                    var oosIds = toDeleteOos.Select(c => c.ContactId).ToList();
                    var failedOos = await mailboxContactGraphService.BatchDeleteContactsAsync(mailbox.UserPrincipalName,
                        folderId!, oosIds, cancellationToken);

                    foreach (var c in toDeleteOos)
                        if (!failedOos.Contains(c.ContactId))
                        {
                            deleted++;
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, c.SourceUserObjectId,
                                SyncAction.Delete, SyncActionResult.Success, "Out-of-scope managed contact deleted.",
                                c.ContactId, null));
                            if (c.SourceUserObjectId.HasValue) syncStateDeletes.Add(c.SourceUserObjectId.Value);
                        }
                        else
                        {
                            mailboxErrors++;
                            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, c.SourceUserObjectId,
                                SyncAction.Error, SyncActionResult.Failed,
                                "Out-of-scope managed contact deletion failed.", c.ContactId, "DeleteFailed"));
                        }

                    // Mark states for contacts that have no managed entry but are also out of scope
                    foreach (var state in mailboxStates.Where(s =>
                                 !s.IsDeleted && !inScopeIds.Contains(s.SourceUserObjectId)))
                        if (toDeleteOos.All(c => c.SourceUserObjectId != state.SourceUserObjectId))
                            syncStateDeletes.Add(state.SourceUserObjectId);
                }
            }

            // Delete legacy folders.
            if (deleteLegacyFolders && mailboxErrors == 0)
                await DeleteLegacyManagedFoldersAsync(mailbox, managedFolderName, legacyFolderNames, allFolders, dryRun,
                    runItems, cancellationToken);

            // Bulk SQL writes.
            if (!dryRun)
            {
                var targetMailboxId = await repository.EnsureTargetMailboxAsync(mailbox, cancellationToken);
                if (syncStateUpdates.Count > 0)
                    await repository.BulkUpsertContactSyncStatesAsync(targetMailboxId, mailbox.UserPrincipalName,
                        syncStateUpdates, cancellationToken);
                if (syncStateDeletes.Count > 0)
                    await repository.BulkMarkSyncStatesDeletedAsync(mailbox.UserPrincipalName, syncStateDeletes,
                        cancellationToken);
            }

            await repository.AddRunItemsBulkAsync(syncRunId, runItems, cancellationToken);
            await repository.UpsertMailboxFolderStateAsync(
                mailbox.UserPrincipalName, managedFolderName, folderId,
                mailboxErrors == 0 ? "Success" : "CompletedWithErrors",
                mailboxErrors == 0 ? null : $"{mailboxErrors} contact-level errors occurred.",
                cancellationToken);

            return new MailboxSyncCounts(created, updated, deleted, skipped, mailboxErrors);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var graphError = GraphExceptionDetails.From(ex);
            runItems.Add(new SyncRunItemDto(
                mailbox.UserPrincipalName, null, SyncAction.Error, SyncActionResult.Failed,
                $"Mailbox sync failed: {graphError.ToSummary()}", null, ex.GetType().Name));
            await repository.AddRunItemsBulkAsync(syncRunId, runItems, CancellationToken.None);
            await repository.UpsertMailboxFolderStateAsync(
                mailbox.UserPrincipalName, managedFolderName, folderId,
                "Failed", graphError.ToSummary(), CancellationToken.None);
            logger.LogError(
                "Mailbox {Mailbox} failed and the sync will continue with the next mailbox. ErrorType={ErrorType} GraphError={GraphError}",
                LogRedactor.Identifier(mailbox.UserPrincipalName, _syncOptions),
                ex.GetType().Name,
                graphError.ToSummary());
            return new MailboxSyncCounts(0, 0, 0, 0, 1);
        }
    }

    private async Task DeleteLegacyManagedFoldersAsync(
        TargetMailbox mailbox,
        string managedFolderName,
        IReadOnlyList<string> legacyFolderNames,
        IReadOnlyList<Microsoft.Graph.Models.ContactFolder> allFolders,
        bool dryRun,
        List<SyncRunItemDto> runItems,
        CancellationToken cancellationToken)
    {
        var foldersToDelete = legacyFolderNames
            .Select(f => f.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Where(f => !string.Equals(f, managedFolderName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var legacyFolderName in foldersToDelete)
        {
            var legacyFolderId = allFolders
                .FirstOrDefault(f => string.Equals(f.DisplayName, legacyFolderName, StringComparison.OrdinalIgnoreCase))
                ?.Id;

            if (string.IsNullOrWhiteSpace(legacyFolderId))
            {
                await repository.RecordLegacyFolderCleanupAsync(mailbox.UserPrincipalName, legacyFolderName, null,
                    "NotFound", cancellationToken);
                runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, null, SyncAction.Cleanup,
                    SyncActionResult.Skipped,
                    $"Legacy contact folder '{legacyFolderName}' was not found.", null, null));
                continue;
            }

            if (dryRun)
                await repository.RecordLegacyFolderCleanupAsync(mailbox.UserPrincipalName, legacyFolderName,
                    legacyFolderId, "SkippedDryRun", cancellationToken);
            else
                try
                {
                    await mailboxContactGraphService.DeleteManagedFolderAsync(mailbox.UserPrincipalName, legacyFolderId,
                        cancellationToken);
                    await repository.RecordLegacyFolderCleanupAsync(mailbox.UserPrincipalName, legacyFolderName,
                        legacyFolderId, "Deleted", cancellationToken);
                }
                catch (Exception ex)
                {
                    var graphError = GraphExceptionDetails.From(ex);
                    await repository.RecordLegacyFolderCleanupAsync(mailbox.UserPrincipalName, legacyFolderName,
                        legacyFolderId, "Failed", cancellationToken);
                    runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, null, SyncAction.Cleanup,
                        SyncActionResult.Failed,
                        $"Legacy contact folder '{legacyFolderName}' deletion failed: {graphError.ToSummary()}",
                        legacyFolderId, ex.GetType().Name));
                    logger.LogError(
                        "Legacy contact folder {LegacyFolderName} deletion failed for mailbox {Mailbox}. ErrorType={ErrorType} GraphError={GraphError}",
                        legacyFolderName,
                        LogRedactor.Identifier(mailbox.UserPrincipalName, _syncOptions),
                        ex.GetType().Name,
                        graphError.ToSummary());
                    continue;
                }

            runItems.Add(new SyncRunItemDto(mailbox.UserPrincipalName, null, SyncAction.Cleanup,
                dryRun ? SyncActionResult.Planned : SyncActionResult.Success,
                dryRun
                    ? $"Dry-run skipped legacy contact folder '{legacyFolderName}' delete after successful sync."
                    : $"Legacy contact folder '{legacyFolderName}' deleted after successful sync.",
                legacyFolderId, null));
        }
    }

    private static IReadOnlyList<DirectoryContact> ApplySourceExclusions(
        IReadOnlyList<DirectoryContact> contacts,
        IReadOnlyList<SyncExclusion> exclusions)
    {
        return contacts.Where(c => !IsSourceExcluded(c, exclusions)).ToArray();
    }

    private static bool IsSourceExcluded(DirectoryContact contact, IReadOnlyList<SyncExclusion> exclusions)
    {
        return exclusions.Any(e =>
            Matches(e, "SourceUserObjectId", contact.SourceUserObjectId.ToString())
            || Matches(e, "UserPrincipalName", contact.UserPrincipalName)
            || Matches(e, "Mail", contact.Email)
            || MatchesDomain(e, contact.Email));
    }

    private static bool HasAtLeastOnePhoneNumber(DirectoryContact contact)
    {
        return !string.IsNullOrWhiteSpace(contact.MobilePhone) || !string.IsNullOrWhiteSpace(contact.DeskPhone);
    }

    private static IReadOnlyList<ManagedMailboxContact> ApplySyncStateMetadata(
        IReadOnlyList<ManagedMailboxContact> contacts,
        IReadOnlyList<ContactSyncState> states)
    {
        var statesByContactId = states
            .Where(s => !s.IsDeleted && !string.IsNullOrWhiteSpace(s.ExchangeContactId))
            .GroupBy(s => s.ExchangeContactId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return contacts
            .Select(contact =>
            {
                if (contact.SourceUserObjectId.HasValue
                    || !statesByContactId.TryGetValue(contact.ContactId, out var state))
                    return contact;

                return contact with
                {
                    SourceUserObjectId = state.SourceUserObjectId,
                    FieldHash = contact.FieldHash ?? state.LastFieldHash
                };
            })
            .ToArray();
    }

    private static bool IsTargetExcluded(TargetMailbox mailbox, IReadOnlyList<SyncExclusion> exclusions)
    {
        return exclusions.Any(e =>
            Matches(e, "TargetMailboxUpn", mailbox.UserPrincipalName)
            || Matches(e, "TargetMailboxMail", mailbox.Mail)
            || MatchesDomain(e, mailbox.Mail));
    }

    private static bool MatchesDomain(SyncExclusion exclusion, string? email)
    {
        if (!string.Equals(exclusion.ExclusionType, "MailDomain", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(email)) return false;
        var domain = email.Split('@').LastOrDefault();
        return string.Equals(domain, exclusion.ExclusionValue.TrimStart('@'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Matches(SyncExclusion exclusion, string type, string? value)
    {
        return string.Equals(exclusion.ExclusionType, type, StringComparison.OrdinalIgnoreCase)
               && string.Equals(exclusion.ExclusionValue, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool? GetBool(IReadOnlyDictionary<string, string> settings, string key)
    {
        return settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string? GetString(IReadOnlyDictionary<string, string> settings, string key)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }

    private static string[]? GetStringList(IReadOnlyDictionary<string, string> settings, string key)
    {
        if (!settings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return null;
        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private IReadOnlyList<string> GetLegacyFolderNames(IReadOnlyDictionary<string, string> settings)
    {
        var configured = GetStringList(settings, "LegacyManagedFolderNames")
                         ?? GetStringList(settings, "LegacyContactFolderName")
                         ?? _syncOptions.LegacyManagedFolderNames;

        return configured
            .Append(_syncOptions.LegacyContactFolderName)
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private SyncWorkerOptions CreateContactMappingOptions(string managedCategory)
    {
        return new SyncWorkerOptions
        {
            ManagedCategory = managedCategory,
            ManagedByMarker = _syncOptions.ManagedByMarker,
            CompanyName = _syncOptions.CompanyName
        };
    }
}
