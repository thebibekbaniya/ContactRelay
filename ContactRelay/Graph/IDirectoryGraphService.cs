using ContactRelay.Models;

namespace ContactRelay.Graph;

public interface IDirectoryGraphService
{
    void ClearRunCache();

    Task<IReadOnlyList<DirectoryContact>> GetPublishedContactsAsync(ISet<Guid> includedSourceIds, CancellationToken cancellationToken);

    Task<IReadOnlyList<TargetMailbox>> GetAllUserMailboxesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TargetMailbox>> GetTargetMailboxesFromGroupAsync(CancellationToken cancellationToken);
}
