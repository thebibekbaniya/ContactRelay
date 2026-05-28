namespace ContactRelay.Options;

public sealed class SyncWorkerOptions
{
    public const string SectionName = "Sync";

    public bool Enabled { get; init; }

    public bool DryRun { get; init; }

    public bool RunOnStartup { get; init; }

    public string? Schedule { get; init; }

    public string? DailyRunTime { get; init; }

    public int IntervalMinutes { get; init; }

    public bool TargetAllUserMailboxes { get; init; }

    public string ManagedFolderName { get; init; } = "";

    public string LegacyContactFolderName { get; init; } = "";

    public string ManagedCategory { get; init; } = "";

    public string ManagedByMarker { get; init; } = "";

    public string CompanyName { get; init; } = "";

    public bool DeleteOutOfScopeContacts { get; init; }

    public bool DeleteLegacyFolderAfterSuccessfulSync { get; init; }

    public bool DeleteLegacyFoldersAfterSuccessfulSync { get; init; }

    public bool ExcludeCommunityMailboxes { get; init; }

    public bool ExcludeSharedMailboxes { get; init; }

    public bool ExcludeServiceAccounts { get; init; }

    public bool ExcludeDisabledAccounts { get; init; }

    public bool ExcludeRoomAndResourceMailboxes { get; init; }

    public string[] LegacyManagedFolderNames { get; init; } = [];

    public string[] ExcludedAccountNamePatterns { get; init; } = [];

    public int MailboxConcurrency { get; init; }

    public int StaleRunTimeoutHours { get; init; }

    public bool LogSensitiveIdentifiers { get; init; }
}
