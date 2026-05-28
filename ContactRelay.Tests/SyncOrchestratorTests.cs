using ContactRelay.Data;
using ContactRelay.Graph;
using ContactRelay.Mapping;
using ContactRelay.Models;
using ContactRelay.Options;
using ContactRelay.Services;
using ContactRelay.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;

namespace ContactRelay.Tests;

public sealed class SyncOrchestratorTests
{
    private const string Mailbox = "mailbox@example.test";

    [Fact]
    public async Task RunAsync_DryRunPlansCreatesWithoutGraphWrites()
    {
        var source = SourceContact();
        var fixture = Fixture(dryRun: true, source);

        var summary = await fixture.Orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(1, summary.CreatedCount);
        Assert.Empty(fixture.MailboxGraph.CreatedContacts);
        Assert.Contains(fixture.Repository.RunItems, item => item.Action == SyncAction.Create && item.Result == SyncActionResult.Planned);
        Assert.Empty(fixture.Repository.SyncStateUpdates);
    }

    [Fact]
    public async Task RunAsync_SkipsCurrentManagedContact()
    {
        var source = SourceContact();
        var fixture = Fixture(dryRun: false, source);
        fixture.MailboxGraph.ManagedContacts =
        [
            new ManagedMailboxContact
            {
                ContactId = "contact-1",
                SourceUserObjectId = source.SourceUserObjectId,
                FieldHash = source.FieldHash,
                ActualFieldHash = source.FieldHash,
                IsManaged = true
            }
        ];
        fixture.Repository.SyncStates =
        [
            new ContactSyncState
            {
                TargetMailboxUpn = Mailbox,
                SourceUserObjectId = source.SourceUserObjectId,
                ExchangeContactId = "contact-1",
                LastFieldHash = source.FieldHash
            }
        ];

        var summary = await fixture.Orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(1, summary.SkippedCount);
        Assert.Empty(fixture.MailboxGraph.UpdatedContacts);
        Assert.Contains(fixture.Repository.RunItems, item => item.Action == SyncAction.Skip && item.Result == SyncActionResult.Skipped);
    }

    [Fact]
    public async Task RunAsync_UpdatesExistingContactWhenHashDiffers()
    {
        var source = SourceContact();
        var fixture = Fixture(dryRun: false, source);
        fixture.MailboxGraph.ManagedContacts =
        [
            new ManagedMailboxContact
            {
                ContactId = "contact-1",
                SourceUserObjectId = source.SourceUserObjectId,
                FieldHash = "old",
                ActualFieldHash = "old",
                IsManaged = true
            }
        ];
        fixture.Repository.SyncStates =
        [
            new ContactSyncState
            {
                TargetMailboxUpn = Mailbox,
                SourceUserObjectId = source.SourceUserObjectId,
                ExchangeContactId = "contact-1",
                LastFieldHash = "old"
            }
        ];

        var summary = await fixture.Orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(1, summary.UpdatedCount);
        Assert.Contains(fixture.MailboxGraph.UpdatedContacts, update => update.ContactId == "contact-1");
        Assert.Contains(fixture.Repository.SyncStateUpdates, update => update.SourceUserObjectId == source.SourceUserObjectId);
    }

    [Fact]
    public async Task RunAsync_PlansDuplicateDeletionInDryRun()
    {
        var source = SourceContact();
        var fixture = Fixture(dryRun: true, source);
        fixture.MailboxGraph.Folders = [new ContactFolder { Id = "folder-1", DisplayName = "ContactRelay" }];
        fixture.MailboxGraph.ManagedContacts =
        [
            new ManagedMailboxContact
            {
                ContactId = "primary",
                SourceUserObjectId = source.SourceUserObjectId,
                FieldHash = source.FieldHash,
                ActualFieldHash = source.FieldHash,
                IsManaged = true
            },
            new ManagedMailboxContact
            {
                ContactId = "duplicate",
                SourceUserObjectId = source.SourceUserObjectId,
                FieldHash = source.FieldHash,
                ActualFieldHash = source.FieldHash,
                IsManaged = true
            }
        ];

        var summary = await fixture.Orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(1, summary.DeletedCount);
        Assert.Empty(fixture.MailboxGraph.DeletedContacts);
        Assert.Contains(fixture.Repository.RunItems, item => item.Action == SyncAction.Delete && item.Result == SyncActionResult.Planned);
    }

    [Fact]
    public async Task RunAsync_IsolatesSingleMailboxFailure()
    {
        var source = SourceContact();
        var fixture = Fixture(dryRun: false, source);
        fixture.Repository.TargetMailboxes =
        [
            TargetMailbox(Mailbox),
            TargetMailbox("second@example.test")
        ];
        fixture.MailboxGraph.FailManagedContactReadFor.Add(Mailbox);

        var summary = await fixture.Orchestrator.RunAsync(CancellationToken.None);

        Assert.Equal(1, summary.ErrorCount);
        Assert.Equal(1, summary.CreatedCount);
        Assert.Contains(fixture.Repository.FolderStatesWritten, state => state.TargetMailboxUpn == Mailbox && state.Status == "Failed");
        Assert.Contains(fixture.Repository.RunItems, item => item.TargetMailboxUpn == Mailbox && item.Action == SyncAction.Error);
    }

    private static TestFixture Fixture(bool dryRun, params DirectoryContact[] sources)
    {
        var repository = new FakeSyncRepository
        {
            Settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["DryRun"] = dryRun.ToString(),
                ["TargetAllUserMailboxes"] = "false",
                ["DeleteOutOfScopeContacts"] = "false"
            },
            TargetMailboxes = [TargetMailbox(Mailbox)]
        };
        var directory = new FakeDirectoryGraphService { PublishedContacts = sources };
        var mailbox = new FakeMailboxContactGraphService();
        var mapper = new ContactMapper();
        var options = Microsoft.Extensions.Options.Options.Create(new SyncWorkerOptions
        {
            Enabled = true,
            DryRun = dryRun,
            ManagedFolderName = "ContactRelay",
            ManagedCategory = "ContactRelay Managed",
            ManagedByMarker = "ManagedBy=ContactRelay",
            MailboxConcurrency = 2,
            StaleRunTimeoutHours = 6
        });
        var orchestrator = new SyncOrchestrator(
            repository,
            directory,
            mailbox,
            mapper,
            options,
            NullLogger<SyncOrchestrator>.Instance);

        return new TestFixture(orchestrator, repository, directory, mailbox);
    }

    private static DirectoryContact SourceContact()
    {
        return SourceContact(Guid.NewGuid(), "person@example.test");
    }

    private static DirectoryContact SourceContact(Guid sourceId, string email)
    {
        var contact = new DirectoryContact
        {
            SourceUserObjectId = sourceId,
            UserPrincipalName = email,
            Email = email,
            FirstName = "First",
            LastName = "Last",
            DisplayName = "First Last",
            MobilePhone = "555-0100",
            AccountEnabled = true
        };

        return contact with
        {
            FieldHash = HashUtility.ComputeSha256(new
            {
                contact.FirstName,
                contact.LastName,
                contact.DisplayName,
                contact.JobTitle,
                contact.Department,
                contact.CompanyName,
                contact.MobilePhone,
                contact.DeskPhone,
                contact.Email
            })
        };
    }

    private static TargetMailbox TargetMailbox(string mail)
    {
        return new TargetMailbox
        {
            UserPrincipalName = mail,
            Mail = mail,
            DisplayName = "Mailbox",
            Source = "Test"
        };
    }

    private sealed record TestFixture(
        SyncOrchestrator Orchestrator,
        FakeSyncRepository Repository,
        FakeDirectoryGraphService Directory,
        FakeMailboxContactGraphService MailboxGraph);

    private sealed class FakeDirectoryGraphService : IDirectoryGraphService
    {
        public IReadOnlyList<DirectoryContact> PublishedContacts { get; init; } = [];

        public void ClearRunCache()
        {
        }

        public Task<IReadOnlyList<DirectoryContact>> GetPublishedContactsAsync(ISet<Guid> includedSourceIds, CancellationToken cancellationToken)
        {
            return Task.FromResult(PublishedContacts);
        }

        public Task<IReadOnlyList<TargetMailbox>> GetAllUserMailboxesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TargetMailbox>>([]);
        }

        public Task<IReadOnlyList<TargetMailbox>> GetTargetMailboxesFromGroupAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TargetMailbox>>([]);
        }
    }

    private sealed class FakeMailboxContactGraphService : IMailboxContactGraphService
    {
        public IReadOnlyList<ContactFolder> Folders { get; set; } = [new ContactFolder { Id = "folder-1", DisplayName = "ContactRelay" }];

        public IReadOnlyList<ManagedMailboxContact> ManagedContacts { get; set; } = [];

        public List<(string Mailbox, Contact Contact)> CreatedContacts { get; } = [];

        public List<(string Mailbox, string ContactId, Contact Contact)> UpdatedContacts { get; } = [];

        public List<string> DeletedContacts { get; } = [];

        public HashSet<string> FailManagedContactReadFor { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<ContactFolder>> GetContactFoldersAsync(string mailboxUpn, CancellationToken cancellationToken)
        {
            return Task.FromResult(Folders);
        }

        public Task<string?> GetManagedFolderIdAsync(string mailboxUpn, string folderName, CancellationToken cancellationToken)
        {
            return Task.FromResult(Folders.FirstOrDefault(f => f.DisplayName == folderName)?.Id);
        }

        public Task<string> EnsureManagedFolderAsync(string mailboxUpn, string folderName, CancellationToken cancellationToken)
        {
            return Task.FromResult("folder-1");
        }

        public Task<string> EnsureManagedFolderAsync(string mailboxUpn, string folderName, IReadOnlyList<ContactFolder> folders, CancellationToken cancellationToken)
        {
            return Task.FromResult(folders.FirstOrDefault(f => f.DisplayName == folderName)?.Id ?? "folder-1");
        }

        public Task DeleteManagedFolderAsync(string mailboxUpn, string folderId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ManagedMailboxContact>> GetManagedContactsAsync(string mailboxUpn, string folderId, string managedCategory, CancellationToken cancellationToken)
        {
            if (FailManagedContactReadFor.Contains(mailboxUpn))
            {
                throw new InvalidOperationException("Simulated mailbox failure.");
            }

            return Task.FromResult(ManagedContacts);
        }

        public Task<string> CreateContactAsync(string mailboxUpn, string folderId, Contact contact, CancellationToken cancellationToken)
        {
            CreatedContacts.Add((mailboxUpn, contact));
            return Task.FromResult($"created-{CreatedContacts.Count}");
        }

        public Task UpdateContactAsync(string mailboxUpn, string folderId, string contactId, Contact contact, CancellationToken cancellationToken)
        {
            UpdatedContacts.Add((mailboxUpn, contactId, contact));
            return Task.CompletedTask;
        }

        public Task DeleteContactAsync(string mailboxUpn, string folderId, string contactId, CancellationToken cancellationToken)
        {
            DeletedContacts.Add(contactId);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<Guid, string?>> BatchCreateContactsAsync(
            string mailboxUpn,
            string folderId,
            IReadOnlyList<(Guid SourceId, Contact Contact)> creates,
            CancellationToken cancellationToken)
        {
            var results = new Dictionary<Guid, string?>();
            foreach (var (sourceId, contact) in creates)
            {
                CreatedContacts.Add((mailboxUpn, contact));
                results[sourceId] = $"created-{CreatedContacts.Count}";
            }

            return Task.FromResult<IReadOnlyDictionary<Guid, string?>>(results);
        }

        public Task<IReadOnlySet<Guid>> BatchUpdateContactsAsync(
            string mailboxUpn,
            string folderId,
            IReadOnlyList<(Guid SourceId, string ContactId, Contact Contact)> updates,
            CancellationToken cancellationToken)
        {
            foreach (var item in updates)
            {
                UpdatedContacts.Add((mailboxUpn, item.ContactId, item.Contact));
            }

            return Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
        }

        public Task<IReadOnlySet<string>> BatchDeleteContactsAsync(
            string mailboxUpn,
            string folderId,
            IReadOnlyList<string> contactIds,
            CancellationToken cancellationToken)
        {
            DeletedContacts.AddRange(contactIds);
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private sealed class FakeSyncRepository : ISyncRepository
    {
        public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>();

        public IReadOnlyList<TargetMailbox> TargetMailboxes { get; set; } = [];

        public IReadOnlyList<ContactSyncState> SyncStates { get; set; } = [];

        public List<SyncRunItemDto> RunItems { get; } = [];

        public List<ContactSyncStateUpdate> SyncStateUpdates { get; } = [];

        public List<(string TargetMailboxUpn, string Status)> FolderStatesWritten { get; } = [];

        public Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Settings);
        }

        public Task<IReadOnlyList<TargetMailbox>> GetEnabledTargetMailboxesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(TargetMailboxes);
        }

        public Task<IReadOnlyList<PublishedContactRecord>> GetPublishedContactOverridesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PublishedContactRecord>>([]);
        }

        public Task<IReadOnlyList<SyncExclusion>> GetEnabledExclusionsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<SyncExclusion>>([]);
        }

        public Task<long> StartRunAsync(bool dryRun, CancellationToken cancellationToken)
        {
            return Task.FromResult(100L);
        }

        public Task MarkStaleRunsAsFailedAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task CompleteRunAsync(long syncRunId, SyncRunSummary summary, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task FailRunAsync(long syncRunId, string error, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpsertPublishedContactsAsync(IEnumerable<DirectoryContact> contacts, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<ILookup<string, ContactSyncState>> GetAllSyncStatesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(SyncStates.ToLookup(s => s.TargetMailboxUpn, StringComparer.OrdinalIgnoreCase));
        }

        public Task<IReadOnlyDictionary<string, MailboxFolderState>> GetMailboxFolderStatesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, MailboxFolderState> states = TargetMailboxes.ToDictionary(
                mailbox => mailbox.UserPrincipalName,
                _ => new MailboxFolderState("folder-1", "Success"),
                StringComparer.OrdinalIgnoreCase);
            return Task.FromResult(states);
        }

        public Task AddRunItemsBulkAsync(long syncRunId, IReadOnlyList<SyncRunItemDto> items, CancellationToken cancellationToken)
        {
            RunItems.AddRange(items);
            return Task.CompletedTask;
        }

        public Task<long> EnsureTargetMailboxAsync(TargetMailbox mailbox, CancellationToken cancellationToken)
        {
            return Task.FromResult(10L);
        }

        public Task BulkUpsertContactSyncStatesAsync(long targetMailboxId, string targetMailboxUpn, IReadOnlyList<ContactSyncStateUpdate> updates, CancellationToken cancellationToken)
        {
            SyncStateUpdates.AddRange(updates);
            return Task.CompletedTask;
        }

        public Task BulkMarkSyncStatesDeletedAsync(string targetMailboxUpn, IReadOnlyList<Guid> sourceUserObjectIds, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task UpsertMailboxFolderStateAsync(string targetMailboxUpn, string managedFolderName, string? managedFolderId, string syncStatus, string? errorMessage, CancellationToken cancellationToken)
        {
            FolderStatesWritten.Add((targetMailboxUpn, syncStatus));
            return Task.CompletedTask;
        }

        public Task RecordLegacyFolderCleanupAsync(string targetMailboxUpn, string legacyFolderName, string? legacyFolderId, string cleanupStatus, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
