using Microsoft.Graph.Models;
using ContactRelay.Models;

namespace ContactRelay.Graph;

public interface IMailboxContactGraphService
{
    Task<IReadOnlyList<ContactFolder>> GetContactFoldersAsync(string mailboxUpn, CancellationToken cancellationToken);

    Task<string?> GetManagedFolderIdAsync(string mailboxUpn, string folderName, CancellationToken cancellationToken);

    Task<string> EnsureManagedFolderAsync(string mailboxUpn, string folderName, CancellationToken cancellationToken);

    Task<string> EnsureManagedFolderAsync(string mailboxUpn, string folderName, IReadOnlyList<ContactFolder> folders, CancellationToken cancellationToken);

    Task DeleteManagedFolderAsync(string mailboxUpn, string folderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ManagedMailboxContact>> GetManagedContactsAsync(string mailboxUpn, string folderId, string managedCategory, CancellationToken cancellationToken);

    Task<string> CreateContactAsync(string mailboxUpn, string folderId, Contact contact, CancellationToken cancellationToken);

    Task UpdateContactAsync(string mailboxUpn, string folderId, string contactId, Contact contact, CancellationToken cancellationToken);

    Task DeleteContactAsync(string mailboxUpn, string folderId, string contactId, CancellationToken cancellationToken);

    // Batch operations fall back to individual calls for any item that fails.
    Task<IReadOnlyDictionary<Guid, string?>> BatchCreateContactsAsync(
        string mailboxUpn,
        string folderId,
        IReadOnlyList<(Guid SourceId, Contact Contact)> creates,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<Guid>> BatchUpdateContactsAsync(
        string mailboxUpn,
        string folderId,
        IReadOnlyList<(Guid SourceId, string ContactId, Contact Contact)> updates,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> BatchDeleteContactsAsync(
        string mailboxUpn,
        string folderId,
        IReadOnlyList<string> contactIds,
        CancellationToken cancellationToken);
}
