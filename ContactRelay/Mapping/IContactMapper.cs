using Microsoft.Graph.Models;
using ContactRelay.Models;
using ContactRelay.Options;

namespace ContactRelay.Mapping;

public interface IContactMapper
{
    DirectoryContact MapUser(User user, string? managerDisplayName, SyncWorkerOptions options);

    Contact ToGraphContact(DirectoryContact source, SyncWorkerOptions options);

    ManagedMailboxContact ToManagedContact(Contact contact, string managedCategory);
}
