using ContactRelay.Options;

namespace ContactRelay.Tests;

public sealed class OptionsValidatorsTests
{
    [Fact]
    public void GraphOptionsValidator_Fails_WhenRequiredValuesAreMissing()
    {
        var result = new GraphOptionsValidator().Validate(null, new GraphOptions());

        Assert.True(result.Failed);
        Assert.Contains("Graph:TenantId is required.", result.Failures);
        Assert.Contains("Graph:ClientId is required.", result.Failures);
        Assert.Contains("Graph authentication requires either Graph:ClientSecret or Graph:CertificateThumbprint.", result.Failures);
    }

    [Fact]
    public void GraphOptionsValidator_Accepts_ClientSecretConfiguration()
    {
        var result = new GraphOptionsValidator().Validate(null, ValidGraphOptions());

        Assert.False(result.Failed);
    }

    [Fact]
    public void GraphOptionsValidator_RejectsPlaceholderSecret()
    {
        var options = ValidGraphOptions();
        options.ClientSecret = "CHANGE_ME";

        var result = new GraphOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Graph:ClientSecret contains a placeholder value.", result.Failures);
    }

    [Fact]
    public void GraphOptionsValidator_RejectsExcessiveUserLookupConcurrency()
    {
        var options = ValidGraphOptions();
        options.UserLookupConcurrency = 65;

        var result = new GraphOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Graph:UserLookupConcurrency must be between 1 and 64.", result.Failures);
    }

    [Fact]
    public void SyncWorkerOptionsValidator_RejectsConflictingManagedAndLegacyFolders()
    {
        var options = new SyncWorkerOptions
        {
            Enabled = true,
            DryRun = true,
            Schedule = "0 0 20 * * *",
            ManagedFolderName = "ContactRelay",
            LegacyContactFolderName = "ContactRelay",
            ManagedCategory = "ContactRelay Managed",
            ManagedByMarker = "ManagedBy=ContactRelay",
            MailboxConcurrency = 1,
            StaleRunTimeoutHours = 6
        };

        var result = new SyncWorkerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Sync:ManagedFolderName must not match Sync:LegacyContactFolderName.", result.Failures);
    }

    [Fact]
    public void SyncWorkerOptionsValidator_RejectsExcessiveMailboxConcurrency()
    {
        var options = new SyncWorkerOptions
        {
            Enabled = true,
            DryRun = true,
            Schedule = "0 0 20 * * *",
            ManagedFolderName = "ContactRelay",
            ManagedCategory = "ContactRelay Managed",
            ManagedByMarker = "ManagedBy=ContactRelay",
            MailboxConcurrency = 65,
            StaleRunTimeoutHours = 6
        };

        var result = new SyncWorkerOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Sync:MailboxConcurrency must be between 1 and 64.", result.Failures);
    }

    [Fact]
    public void SqlOptionsValidator_Fails_WhenConnectionStringIsMissing()
    {
        var result = new SqlOptionsValidator().Validate(null, new SqlOptions());

        Assert.True(result.Failed);
        Assert.Contains("Sql:ConnectionString is required.", result.Failures);
    }

    private static GraphOptions ValidGraphOptions()
    {
        return new GraphOptions
        {
            TenantId = "tenant-id",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            CertificateStoreLocation = "LocalMachine",
            PageSize = 999,
            BatchSize = 20,
            MaxRetryAttempts = 3,
            BaseRetryDelaySeconds = 1,
            UserLookupConcurrency = 2,
            Scopes = ["https://graph.microsoft.com/.default"]
        };
    }

    private static SyncWorkerOptions ValidSyncOptions()
    {
        return new SyncWorkerOptions
        {
            Enabled = true,
            DryRun = true,
            Schedule = "0 0 20 * * *",
            ManagedFolderName = "ContactRelay",
            ManagedCategory = "ContactRelay Managed",
            ManagedByMarker = "ManagedBy=ContactRelay",
            MailboxConcurrency = 1,
            StaleRunTimeoutHours = 6
        };
    }
}
