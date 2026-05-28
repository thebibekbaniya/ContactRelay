using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using ContactRelay.Mapping;
using ContactRelay.Models;
using ContactRelay.Options;
using ContactRelay.Utilities;

namespace ContactRelay.Graph;

public sealed class MailboxContactGraphService(
    GraphServiceClient graph,
    IGraphRetryHandler retryHandler,
    IContactMapper mapper,
    IOptions<GraphOptions> options,
    IOptions<SyncWorkerOptions> syncOptions,
    ILogger<MailboxContactGraphService> logger) : IMailboxContactGraphService
{
    private readonly GraphOptions _options = options.Value;
    private readonly SyncWorkerOptions _syncOptions = syncOptions.Value;

    public async Task<string?> GetManagedFolderIdAsync(string mailboxUpn, string folderName, CancellationToken cancellationToken)
    {
        var folders = await GetContactFoldersAsync(mailboxUpn, cancellationToken);
        return folders
            .FirstOrDefault(folder => string.Equals(folder.DisplayName, folderName, StringComparison.OrdinalIgnoreCase))
            ?.Id;
    }

    public async Task<string> EnsureManagedFolderAsync(string mailboxUpn, string folderName, CancellationToken cancellationToken)
    {
        var folders = await GetContactFoldersAsync(mailboxUpn, cancellationToken);
        return await EnsureManagedFolderAsync(mailboxUpn, folderName, folders, cancellationToken);
    }

    public async Task<string> EnsureManagedFolderAsync(
        string mailboxUpn,
        string folderName,
        IReadOnlyList<ContactFolder> folders,
        CancellationToken cancellationToken)
    {
        var existingFolderId = folders
            .FirstOrDefault(folder => string.Equals(folder.DisplayName, folderName, StringComparison.OrdinalIgnoreCase))
            ?.Id;

        if (!string.IsNullOrWhiteSpace(existingFolderId))
            return existingFolderId;

        var created = await retryHandler.ExecuteAsync(
            ct => graph.Users[mailboxUpn].ContactFolders.PostAsync(
                new ContactFolder { DisplayName = folderName },
                cancellationToken: ct),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(created?.Id))
            throw new InvalidOperationException("Microsoft Graph did not return an id after creating the managed contact folder.");

        logger.LogInformation(
            "Created managed contact folder {FolderName} for mailbox {Mailbox}.",
            folderName,
            LogRedactor.Identifier(mailboxUpn, _syncOptions));
        return created.Id;
    }

    public Task DeleteManagedFolderAsync(string mailboxUpn, string folderId, CancellationToken cancellationToken)
    {
        return retryHandler.ExecuteAsync(
            ct => graph.Users[mailboxUpn].ContactFolders[folderId].DeleteAsync(cancellationToken: ct),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ManagedMailboxContact>> GetManagedContactsAsync(
        string mailboxUpn,
        string folderId,
        string managedCategory,
        CancellationToken cancellationToken)
    {
        var contacts = new List<ManagedMailboxContact>();
        var page = await retryHandler.ExecuteAsync(
            ct => graph.Users[mailboxUpn].ContactFolders[folderId].Contacts.GetAsync(request =>
            {
                request.QueryParameters.Top = _options.PageSize;
                request.QueryParameters.Select =
                [
                    "id", "displayName", "givenName", "surname", "companyName", "jobTitle",
                    "department", "mobilePhone", "businessPhones", "emailAddresses",
                    "categories", "personalNotes"
                ];
            }, ct),
            cancellationToken);

        while (page is not null)
        {
            if (page.Value is not null)
            {
                foreach (var contact in page.Value)
                {
                    var managed = mapper.ToManagedContact(contact, managedCategory);
                    if (managed.IsManaged && !string.IsNullOrWhiteSpace(managed.ContactId))
                        contacts.Add(managed);
                }
            }

            if (string.IsNullOrWhiteSpace(page.OdataNextLink))
                break;

            var nextLink = page.OdataNextLink;
            page = await retryHandler.ExecuteAsync(
                ct => graph.Users[mailboxUpn].ContactFolders[folderId].Contacts.WithUrl(nextLink).GetAsync(cancellationToken: ct),
                cancellationToken);
        }

        return contacts;
    }

    public async Task<string> CreateContactAsync(string mailboxUpn, string folderId, Contact contact, CancellationToken cancellationToken)
    {
        var created = await retryHandler.ExecuteAsync(
            ct => graph.Users[mailboxUpn].ContactFolders[folderId].Contacts.PostAsync(contact, cancellationToken: ct),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(created?.Id))
            throw new InvalidOperationException("Microsoft Graph did not return a contact id after creating a managed contact.");

        return created.Id;
    }

    public Task UpdateContactAsync(string mailboxUpn, string folderId, string contactId, Contact contact, CancellationToken cancellationToken)
    {
        return retryHandler.ExecuteAsync(
            ct => graph.Users[mailboxUpn].ContactFolders[folderId].Contacts[contactId].PatchAsync(contact, cancellationToken: ct),
            cancellationToken);
    }

    public Task DeleteContactAsync(string mailboxUpn, string folderId, string contactId, CancellationToken cancellationToken)
    {
        return retryHandler.ExecuteAsync(
            ct => graph.Users[mailboxUpn].ContactFolders[folderId].Contacts[contactId].DeleteAsync(cancellationToken: ct),
            cancellationToken);
    }

    public async Task<IReadOnlyList<ContactFolder>> GetContactFoldersAsync(string mailboxUpn, CancellationToken cancellationToken)
    {
        var folders = new List<ContactFolder>();
        var page = await retryHandler.ExecuteAsync(
            ct => graph.Users[mailboxUpn].ContactFolders.GetAsync(request =>
            {
                request.QueryParameters.Top = _options.PageSize;
                request.QueryParameters.Select = ["id", "displayName"];
            }, ct),
            cancellationToken);

        while (page is not null)
        {
            if (page.Value is not null)
                folders.AddRange(page.Value);

            if (string.IsNullOrWhiteSpace(page.OdataNextLink))
                break;

            var nextLink = page.OdataNextLink;
            page = await retryHandler.ExecuteAsync(
                ct => graph.Users[mailboxUpn].ContactFolders.WithUrl(nextLink).GetAsync(cancellationToken: ct),
                cancellationToken);
        }

        return folders;
    }

    // Batch operations
    // Each batch sends up to BatchSize (20) requests in a single HTTP call.
    // Individual item failures fall back to the existing per-item retry methods.

    public async Task<IReadOnlyDictionary<Guid, string?>> BatchCreateContactsAsync(
        string mailboxUpn,
        string folderId,
        IReadOnlyList<(Guid SourceId, Contact Contact)> creates,
        CancellationToken cancellationToken)
    {
        var results = new Dictionary<Guid, string?>();
        if (creates.Count == 0) return results;

        foreach (var chunk in creates.Chunk(_options.BatchSize))
        {
            var batchContent = new BatchRequestContentCollection(graph);
            var stepToSource = new Dictionary<string, Guid>();
            var sourceToContact = chunk.ToDictionary(c => c.SourceId, c => c.Contact);

            foreach (var (sourceId, contact) in chunk)
            {
                var requestInfo = graph.Users[mailboxUpn].ContactFolders[folderId].Contacts
                    .ToPostRequestInformation(contact);
                var stepId = await batchContent.AddBatchRequestStepAsync(requestInfo);
                stepToSource[stepId] = sourceId;
            }

            BatchResponseContentCollection? batchResponse = null;
            try
            {
                batchResponse = await retryHandler.ExecuteAsync(
                    ct => graph.Batch.PostAsync(batchContent, ct),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                var graphError = GraphExceptionDetails.From(ex);
                logger.LogWarning(
                    "Batch create for {Mailbox} failed entirely; falling back to individual calls for {Count} contacts. ErrorType={ErrorType} GraphError={GraphError}",
                    LogRedactor.Identifier(mailboxUpn, _syncOptions), chunk.Length, ex.GetType().Name, graphError.ToSummary());
            }

            foreach (var (stepId, sourceId) in stepToSource)
            {
                if (batchResponse is not null)
                {
                    try
                    {
                        var created = await batchResponse.GetResponseByIdAsync<Contact>(stepId);
                        if (!string.IsNullOrWhiteSpace(created?.Id))
                        {
                            results[sourceId] = created.Id;
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        var graphError = GraphExceptionDetails.From(ex);
                        logger.LogWarning(
                            "Batch create item failed for source {SourceId} in {Mailbox}; falling back to individual call. ErrorType={ErrorType} GraphError={GraphError}",
                            LogRedactor.Identifier(sourceId, _syncOptions),
                            LogRedactor.Identifier(mailboxUpn, _syncOptions),
                            ex.GetType().Name,
                            graphError.ToSummary());
                    }
                }

                // Fall back to individual call with retry
                try
                {
                    results[sourceId] = await CreateContactAsync(mailboxUpn, folderId, sourceToContact[sourceId], cancellationToken);
                }
                catch (Exception ex)
                {
                    var graphError = GraphExceptionDetails.From(ex);
                    logger.LogWarning(
                        "Individual create fallback failed for source {SourceId} in {Mailbox}. ErrorType={ErrorType} GraphError={GraphError}",
                        LogRedactor.Identifier(sourceId, _syncOptions),
                        LogRedactor.Identifier(mailboxUpn, _syncOptions),
                        ex.GetType().Name,
                        graphError.ToSummary());
                    results[sourceId] = null;
                }
            }
        }

        return results;
    }

    public async Task<IReadOnlySet<Guid>> BatchUpdateContactsAsync(
        string mailboxUpn,
        string folderId,
        IReadOnlyList<(Guid SourceId, string ContactId, Contact Contact)> updates,
        CancellationToken cancellationToken)
    {
        var failed = new HashSet<Guid>();
        if (updates.Count == 0) return failed;

        foreach (var chunk in updates.Chunk(_options.BatchSize))
        {
            var batchContent = new BatchRequestContentCollection(graph);
            var stepToSource = new Dictionary<string, Guid>();
            var sourceToItem = chunk.ToDictionary(u => u.SourceId, u => u);

            foreach (var (sourceId, contactId, contact) in chunk)
            {
                var requestInfo = graph.Users[mailboxUpn].ContactFolders[folderId].Contacts[contactId]
                    .ToPatchRequestInformation(contact);
                var stepId = await batchContent.AddBatchRequestStepAsync(requestInfo);
                stepToSource[stepId] = sourceId;
            }

            BatchResponseContentCollection? batchResponse = null;
            try
            {
                batchResponse = await retryHandler.ExecuteAsync(
                    ct => graph.Batch.PostAsync(batchContent, ct),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                var graphError = GraphExceptionDetails.From(ex);
                logger.LogWarning(
                    "Batch update for {Mailbox} failed entirely; falling back to individual calls for {Count} contacts. ErrorType={ErrorType} GraphError={GraphError}",
                    LogRedactor.Identifier(mailboxUpn, _syncOptions), chunk.Length, ex.GetType().Name, graphError.ToSummary());
            }

            foreach (var (stepId, sourceId) in stepToSource)
            {
                if (batchResponse is not null)
                {
                    try
                    {
                        // PATCH returns 200 or 204. GetResponseByIdAsync returns null for 204.
                        _ = await batchResponse.GetResponseByIdAsync<Contact>(stepId);
                        continue; // success
                    }
                    catch (Exception ex)
                    {
                        var graphError = GraphExceptionDetails.From(ex);
                        logger.LogWarning(
                            "Batch update item failed for source {SourceId} in {Mailbox}; falling back to individual call. ErrorType={ErrorType} GraphError={GraphError}",
                            LogRedactor.Identifier(sourceId, _syncOptions),
                            LogRedactor.Identifier(mailboxUpn, _syncOptions),
                            ex.GetType().Name,
                            graphError.ToSummary());
                    }
                }

                // Fall back to individual call with retry
                var item = sourceToItem[sourceId];
                try
                {
                    await UpdateContactAsync(mailboxUpn, folderId, item.ContactId, item.Contact, cancellationToken);
                }
                catch (Exception ex)
                {
                    var graphError = GraphExceptionDetails.From(ex);
                    logger.LogWarning(
                        "Individual update fallback failed for source {SourceId} in {Mailbox}. ErrorType={ErrorType} GraphError={GraphError}",
                        LogRedactor.Identifier(sourceId, _syncOptions),
                        LogRedactor.Identifier(mailboxUpn, _syncOptions),
                        ex.GetType().Name,
                        graphError.ToSummary());
                    failed.Add(sourceId);
                }
            }
        }

        return failed;
    }

    public async Task<IReadOnlySet<string>> BatchDeleteContactsAsync(
        string mailboxUpn,
        string folderId,
        IReadOnlyList<string> contactIds,
        CancellationToken cancellationToken)
    {
        var failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (contactIds.Count == 0) return failed;

        foreach (var chunk in contactIds.Chunk(_options.BatchSize))
        {
            var batchContent = new BatchRequestContentCollection(graph);
            var stepToContact = new Dictionary<string, string>();

            foreach (var contactId in chunk)
            {
                var requestInfo = graph.Users[mailboxUpn].ContactFolders[folderId].Contacts[contactId]
                    .ToDeleteRequestInformation();
                var stepId = await batchContent.AddBatchRequestStepAsync(requestInfo);
                stepToContact[stepId] = contactId;
            }

            BatchResponseContentCollection? batchResponse = null;
            try
            {
                batchResponse = await retryHandler.ExecuteAsync(
                    ct => graph.Batch.PostAsync(batchContent, ct),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                var graphError = GraphExceptionDetails.From(ex);
                logger.LogWarning(
                    "Batch delete for {Mailbox} failed entirely; falling back to individual calls for {Count} contacts. ErrorType={ErrorType} GraphError={GraphError}",
                    LogRedactor.Identifier(mailboxUpn, _syncOptions), chunk.Length, ex.GetType().Name, graphError.ToSummary());
            }

            foreach (var (stepId, contactId) in stepToContact)
            {
                if (batchResponse is not null)
                {
                    try
                    {
                        // DELETE returns 204 No Content. A null return is expected and means success.
                        _ = await batchResponse.GetResponseByIdAsync<Contact>(stepId);
                        continue; // success
                    }
                    catch (Exception ex)
                    {
                        var graphError = GraphExceptionDetails.From(ex);
                        logger.LogWarning(
                            "Batch delete item failed for contact {ContactId} in {Mailbox}; falling back to individual call. ErrorType={ErrorType} GraphError={GraphError}",
                            LogRedactor.Identifier(contactId, _syncOptions),
                            LogRedactor.Identifier(mailboxUpn, _syncOptions),
                            ex.GetType().Name,
                            graphError.ToSummary());
                    }
                }

                // Fall back to individual call with retry
                try
                {
                    await DeleteContactAsync(mailboxUpn, folderId, contactId, cancellationToken);
                }
                catch (Exception ex)
                {
                    var graphError = GraphExceptionDetails.From(ex);
                    logger.LogWarning(
                        "Individual delete fallback failed for contact {ContactId} in {Mailbox}. ErrorType={ErrorType} GraphError={GraphError}",
                        LogRedactor.Identifier(contactId, _syncOptions),
                        LogRedactor.Identifier(mailboxUpn, _syncOptions),
                        ex.GetType().Name,
                        graphError.ToSummary());
                    failed.Add(contactId);
                }
            }
        }

        return failed;
    }
}
