using Dapper;
using Microsoft.Extensions.Options;
using ContactRelay.Models;
using ContactRelay.Options;
using ContactRelay.Utilities;

namespace ContactRelay.Data;

file sealed class MailboxFolderRow
{
    public string TargetMailboxUpn { get; set; } = "";
    public string? ManagedFolderId { get; set; }
    public string? LastMailboxSyncStatus { get; set; }
}

public sealed class SyncRepository(
    ISqlConnectionFactory connectionFactory,
    IOptions<SqlOptions> options,
    IOptions<SyncWorkerOptions> syncOptions,
    ILogger<SyncRepository> logger) : ISyncRepository
{
    private readonly int _commandTimeoutSeconds = options.Value.CommandTimeoutSeconds;
    private readonly int _staleRunTimeoutHours = syncOptions.Value.StaleRunTimeoutHours;
    private readonly SyncWorkerOptions _syncOptions = syncOptions.Value;

    public async Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SettingKey, SettingValue
            FROM dbo.SyncSettings
            WHERE IsEnabled = 1;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        var rows = await connection.QueryAsync<(string SettingKey, string? SettingValue)>(command);
        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.SettingValue))
            .ToDictionary(row => row.SettingKey, row => row.SettingValue!, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<TargetMailbox>> GetEnabledTargetMailboxesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                TargetMailboxId,
                EntraUserObjectId,
                UserPrincipalName,
                Mail,
                DisplayName,
                IsEnabled,
                'Sql' AS Source
            FROM dbo.TargetMailbox
            WHERE IsEnabled = 1
              AND IsDeleted = 0
              AND Mail IS NOT NULL
              AND LTRIM(RTRIM(Mail)) <> '';
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        return (await connection.QueryAsync<TargetMailbox>(command)).AsList();
    }

    public async Task<IReadOnlyList<PublishedContactRecord>> GetPublishedContactOverridesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                PublishedContactId,
                SourceUserObjectId,
                UserPrincipalName,
                Mail,
                DisplayName,
                FieldHash,
                IsEnabled,
                IsDeleted,
                IsManualOverride
            FROM dbo.PublishedContact
            WHERE IsManualOverride = 1
              AND IsEnabled = 1
              AND IsDeleted = 0;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        return (await connection.QueryAsync<PublishedContactRecord>(command)).AsList();
    }

    public async Task<IReadOnlyList<SyncExclusion>> GetEnabledExclusionsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT SyncExclusionId, ExclusionType, ExclusionValue, IsEnabled
            FROM dbo.SyncExclusion
            WHERE IsEnabled = 1;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        return (await connection.QueryAsync<SyncExclusion>(command)).AsList();
    }

    public async Task<long> StartRunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT dbo.SyncRun (StartedUtc, Status, IsDryRun, HostName, ProcessId)
            OUTPUT INSERTED.SyncRunId
            VALUES (SYSUTCDATETIME(), 'Running', @DryRun, HOST_NAME(), @ProcessId);
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { DryRun = dryRun, Environment.ProcessId },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    public async Task MarkStaleRunsAsFailedAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.SyncRun
            SET Status = 'Failed',
                ErrorMessage = 'Service restarted before run completed.',
                CompletedUtc = SYSUTCDATETIME(),
                UpdatedUtc = SYSUTCDATETIME()
            WHERE Status = 'Running'
              AND StartedUtc < DATEADD(HOUR, -@StaleRunTimeoutHours, SYSUTCDATETIME());
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { StaleRunTimeoutHours = _staleRunTimeoutHours },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        var affected = await connection.ExecuteAsync(command);
        if (affected > 0)
        {
            logger.LogWarning("Marked {Count} stale SyncRun records as Failed.", affected);
        }
    }

    public async Task CompleteRunAsync(long syncRunId, SyncRunSummary summary, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.SyncRun
            SET
                CompletedUtc = SYSUTCDATETIME(),
                Status = CASE WHEN @ErrorCount > 0 THEN 'CompletedWithErrors' ELSE 'Completed' END,
                TargetMailboxCount = @TargetMailboxCount,
                PublishedContactCount = @PublishedContactCount,
                CreatedCount = @CreatedCount,
                UpdatedCount = @UpdatedCount,
                DeletedCount = @DeletedCount,
                SkippedCount = @SkippedCount,
                ErrorCount = @ErrorCount,
                UpdatedUtc = SYSUTCDATETIME()
            WHERE SyncRunId = @SyncRunId;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, summary, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task FailRunAsync(long syncRunId, string error, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.SyncRun
            SET CompletedUtc = SYSUTCDATETIME(),
                Status = 'Failed',
                ErrorCount = ErrorCount + 1,
                ErrorMessage = LEFT(@Error, 4000),
                UpdatedUtc = SYSUTCDATETIME()
            WHERE SyncRunId = @SyncRunId;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { SyncRunId = syncRunId, Error = error },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task AddRunItemAsync(
        long syncRunId,
        string? targetMailboxUpn,
        Guid? sourceUserObjectId,
        SyncAction action,
        SyncActionResult result,
        string message,
        string? graphContactId,
        string? errorCode,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT dbo.SyncRunItem
            (
                SyncRunId,
                TargetMailboxUpn,
                SourceUserObjectId,
                Action,
                Result,
                GraphContactId,
                ErrorCode,
                Message,
                CreatedUtc
            )
            VALUES
            (
                @SyncRunId,
                @TargetMailboxUpn,
                @SourceUserObjectId,
                @Action,
                @Result,
                @GraphContactId,
                @ErrorCode,
                LEFT(@Message, 4000),
                SYSUTCDATETIME()
            );
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                SyncRunId = syncRunId,
                TargetMailboxUpn = targetMailboxUpn,
                SourceUserObjectId = sourceUserObjectId,
                Action = action.ToString(),
                Result = result.ToString(),
                GraphContactId = graphContactId,
                ErrorCode = errorCode,
                Message = message
            },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task UpsertPublishedContactsAsync(IEnumerable<DirectoryContact> contacts, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.PublishedContact WITH (HOLDLOCK) AS target
            USING
            (
                SELECT
                    @SourceUserObjectId AS SourceUserObjectId,
                    @UserPrincipalName AS UserPrincipalName,
                    @Mail AS Mail,
                    @DisplayName AS DisplayName,
                    @FirstName AS FirstName,
                    @LastName AS LastName,
                    @JobTitle AS JobTitle,
                    @Department AS Department,
                    @CompanyName AS CompanyName,
                    @MobilePhone AS MobilePhone,
                    @DeskPhone AS DeskPhone,
                    @Manager AS Manager,
                    @EmployeeNumber AS EmployeeNumber,
                    @FieldHash AS FieldHash
            ) AS source
            ON target.SourceUserObjectId = source.SourceUserObjectId
            WHEN MATCHED THEN
                UPDATE SET
                    UserPrincipalName = source.UserPrincipalName,
                    Mail = source.Mail,
                    DisplayName = source.DisplayName,
                    FirstName = source.FirstName,
                    LastName = source.LastName,
                    JobTitle = source.JobTitle,
                    Department = source.Department,
                    CompanyName = source.CompanyName,
                    MobilePhone = source.MobilePhone,
                    DeskPhone = source.DeskPhone,
                    Manager = source.Manager,
                    EmployeeNumber = source.EmployeeNumber,
                    FieldHash = source.FieldHash,
                    IsDeleted = 0,
                    LastSeenUtc = SYSUTCDATETIME(),
                    UpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT
                (
                    SourceUserObjectId,
                    UserPrincipalName,
                    Mail,
                    DisplayName,
                    FirstName,
                    LastName,
                    JobTitle,
                    Department,
                    CompanyName,
                    MobilePhone,
                    DeskPhone,
                    Manager,
                    EmployeeNumber,
                    FieldHash,
                    IsEnabled,
                    IsDeleted,
                    IsManualOverride,
                    LastSeenUtc,
                    CreatedUtc,
                    UpdatedUtc
                )
                VALUES
                (
                    source.SourceUserObjectId,
                    source.UserPrincipalName,
                    source.Mail,
                    source.DisplayName,
                    source.FirstName,
                    source.LastName,
                    source.JobTitle,
                    source.Department,
                    source.CompanyName,
                    source.MobilePhone,
                    source.DeskPhone,
                    source.Manager,
                    source.EmployeeNumber,
                    source.FieldHash,
                    1,
                    0,
                    0,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var contact in contacts)
            {
                var command = new CommandDefinition(
                    sql,
                    new
                    {
                        contact.SourceUserObjectId,
                        contact.UserPrincipalName,
                        Mail = contact.Email,
                        contact.DisplayName,
                        contact.FirstName,
                        contact.LastName,
                        contact.JobTitle,
                        contact.Department,
                        contact.CompanyName,
                        contact.MobilePhone,
                        contact.DeskPhone,
                        contact.Manager,
                        contact.EmployeeNumber,
                        contact.FieldHash
                    },
                    transaction,
                    cancellationToken: cancellationToken,
                    commandTimeout: _commandTimeoutSeconds);

                await connection.ExecuteAsync(command);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ContactSyncState>> GetSyncStatesForMailboxAsync(string targetMailboxUpn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                ContactSyncStateId,
                TargetMailboxId,
                TargetMailboxUpn,
                SourceUserObjectId,
                ExchangeContactId,
                LastFieldHash,
                IsDeleted,
                LastSyncedUtc
            FROM dbo.ContactSyncState
            WHERE TargetMailboxUpn = @TargetMailboxUpn;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { TargetMailboxUpn = targetMailboxUpn },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        return (await connection.QueryAsync<ContactSyncState>(command)).AsList();
    }

    public async Task UpsertSyncStateAsync(
        TargetMailbox targetMailbox,
        Guid sourceUserObjectId,
        string graphContactId,
        string fieldHash,
        CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.TargetMailbox WITH (HOLDLOCK) AS mailboxTarget
            USING
            (
                SELECT
                    @EntraUserObjectId AS EntraUserObjectId,
                    @TargetMailboxUpn AS UserPrincipalName,
                    @TargetMailboxMail AS Mail,
                    @DisplayName AS DisplayName,
                    @FilterSource AS FilterSource
            ) AS mailboxSource
            ON mailboxTarget.UserPrincipalName = mailboxSource.UserPrincipalName
               OR mailboxTarget.Mail = mailboxSource.Mail
            WHEN MATCHED THEN
                UPDATE SET
                    EntraUserObjectId = COALESCE(mailboxSource.EntraUserObjectId, mailboxTarget.EntraUserObjectId),
                    UserPrincipalName = mailboxSource.UserPrincipalName,
                    Mail = mailboxSource.Mail,
                    DisplayName = mailboxSource.DisplayName,
                    FilterSource = mailboxSource.FilterSource,
                    IsEnabled = 1,
                    IsDeleted = 0,
                    UpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT
                (
                    EntraUserObjectId,
                    UserPrincipalName,
                    Mail,
                    DisplayName,
                    FilterSource,
                    IsEnabled,
                    IsDeleted,
                    CreatedUtc,
                    UpdatedUtc
                )
                VALUES
                (
                    mailboxSource.EntraUserObjectId,
                    mailboxSource.UserPrincipalName,
                    mailboxSource.Mail,
                    mailboxSource.DisplayName,
                    mailboxSource.FilterSource,
                    1,
                    0,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );

            DECLARE @TargetMailboxId bigint =
            (
                SELECT TOP (1) TargetMailboxId
                FROM dbo.TargetMailbox
                WHERE UserPrincipalName = @TargetMailboxUpn OR Mail = @TargetMailboxMail
                ORDER BY TargetMailboxId
            );

            MERGE dbo.ContactSyncState WITH (HOLDLOCK) AS target
            USING
            (
                SELECT
                    @TargetMailboxId AS TargetMailboxId,
                    @TargetMailboxUpn AS TargetMailboxUpn,
                    @SourceUserObjectId AS SourceUserObjectId,
                    @ExchangeContactId AS ExchangeContactId,
                    @LastFieldHash AS LastFieldHash
            ) AS source
            ON target.TargetMailboxUpn = source.TargetMailboxUpn
               AND target.SourceUserObjectId = source.SourceUserObjectId
            WHEN MATCHED THEN
                UPDATE SET
                    ExchangeContactId = source.ExchangeContactId,
                    LastFieldHash = source.LastFieldHash,
                    IsDeleted = 0,
                    LastSyncedUtc = SYSUTCDATETIME(),
                    UpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT
                (
                    TargetMailboxId,
                    TargetMailboxUpn,
                    SourceUserObjectId,
                    ExchangeContactId,
                    LastFieldHash,
                    IsDeleted,
                    LastSyncedUtc,
                    CreatedUtc,
                    UpdatedUtc
                )
                VALUES
                (
                    source.TargetMailboxId,
                    source.TargetMailboxUpn,
                    source.SourceUserObjectId,
                    source.ExchangeContactId,
                    source.LastFieldHash,
                    0,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                TargetMailboxUpn = targetMailbox.UserPrincipalName,
                TargetMailboxMail = targetMailbox.Mail,
                targetMailbox.EntraUserObjectId,
                targetMailbox.DisplayName,
                FilterSource = targetMailbox.Source,
                SourceUserObjectId = sourceUserObjectId,
                ExchangeContactId = graphContactId,
                LastFieldHash = fieldHash
            },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task MarkSyncStateDeletedAsync(string targetMailboxUpn, Guid sourceUserObjectId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.ContactSyncState
            SET IsDeleted = 1,
                DeletedUtc = SYSUTCDATETIME(),
                UpdatedUtc = SYSUTCDATETIME()
            WHERE TargetMailboxUpn = @TargetMailboxUpn
              AND SourceUserObjectId = @SourceUserObjectId;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new { TargetMailboxUpn = targetMailboxUpn, SourceUserObjectId = sourceUserObjectId },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        var affected = await connection.ExecuteAsync(command);
        if (affected == 0)
        {
            logger.LogDebug(
                "No sync state row existed to mark deleted for mailbox {Mailbox} source {SourceUserObjectId}.",
                LogRedactor.Identifier(targetMailboxUpn, _syncOptions),
                LogRedactor.Identifier(sourceUserObjectId, _syncOptions));
        }
    }

    public async Task UpsertMailboxFolderStateAsync(
        string targetMailboxUpn,
        string managedFolderName,
        string? managedFolderId,
        string syncStatus,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.MailboxFolderSyncState WITH (HOLDLOCK) AS target
            USING
            (
                SELECT
                    @TargetMailboxUpn AS TargetMailboxUpn,
                    @ManagedFolderName AS ManagedFolderName,
                    @ManagedFolderId AS ManagedFolderId,
                    @LastMailboxSyncStatus AS LastMailboxSyncStatus,
                    @LastErrorMessage AS LastErrorMessage
            ) AS source
            ON target.TargetMailboxUpn = source.TargetMailboxUpn
            WHEN MATCHED THEN
                UPDATE SET
                    ManagedFolderName = source.ManagedFolderName,
                    ManagedFolderId = source.ManagedFolderId,
                    LastMailboxSyncStatus = source.LastMailboxSyncStatus,
                    LastSuccessfulSyncUtc = CASE WHEN source.LastMailboxSyncStatus = 'Success' THEN SYSUTCDATETIME() ELSE target.LastSuccessfulSyncUtc END,
                    LastFailedSyncUtc = CASE WHEN source.LastMailboxSyncStatus IN ('Failed', 'CompletedWithErrors') THEN SYSUTCDATETIME() ELSE target.LastFailedSyncUtc END,
                    LastErrorMessage = LEFT(source.LastErrorMessage, 4000),
                    IsDeleted = 0,
                    UpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT
                (
                    TargetMailboxUpn,
                    ManagedFolderName,
                    ManagedFolderId,
                    LastMailboxSyncStatus,
                    LastSuccessfulSyncUtc,
                    LastFailedSyncUtc,
                    LastErrorMessage,
                    CreatedUtc,
                    UpdatedUtc
                )
                VALUES
                (
                    source.TargetMailboxUpn,
                    source.ManagedFolderName,
                    source.ManagedFolderId,
                    source.LastMailboxSyncStatus,
                    CASE WHEN source.LastMailboxSyncStatus = 'Success' THEN SYSUTCDATETIME() ELSE NULL END,
                    CASE WHEN source.LastMailboxSyncStatus IN ('Failed', 'CompletedWithErrors') THEN SYSUTCDATETIME() ELSE NULL END,
                    LEFT(source.LastErrorMessage, 4000),
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                TargetMailboxUpn = targetMailboxUpn,
                ManagedFolderName = managedFolderName,
                ManagedFolderId = managedFolderId,
                LastMailboxSyncStatus = syncStatus,
                LastErrorMessage = errorMessage
            },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task<ILookup<string, ContactSyncState>> GetAllSyncStatesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT ContactSyncStateId, TargetMailboxId, TargetMailboxUpn, SourceUserObjectId,
                   ExchangeContactId, LastFieldHash, IsDeleted, LastSyncedUtc
            FROM dbo.ContactSyncState;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        var rows = await connection.QueryAsync<ContactSyncState>(command);
        return rows.ToLookup(s => s.TargetMailboxUpn, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, MailboxFolderState>> GetMailboxFolderStatesAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TargetMailboxUpn, ManagedFolderId, LastMailboxSyncStatus
            FROM dbo.MailboxFolderSyncState
            WHERE IsDeleted = 0;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        var rows = await connection.QueryAsync<MailboxFolderRow>(command);
        return rows.ToDictionary(
            r => r.TargetMailboxUpn,
            r => new MailboxFolderState(r.ManagedFolderId, r.LastMailboxSyncStatus),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task AddRunItemsBulkAsync(long syncRunId, IReadOnlyList<SyncRunItemDto> items, CancellationToken cancellationToken)
    {
        if (items.Count == 0) return;

        const string sql = """
            INSERT dbo.SyncRunItem
            (SyncRunId, TargetMailboxUpn, SourceUserObjectId, Action, Result, GraphContactId, ErrorCode, Message, CreatedUtc)
            VALUES
            (@SyncRunId, @TargetMailboxUpn, @SourceUserObjectId, @Action, @Result, @GraphContactId, @ErrorCode, LEFT(@Message, 4000), SYSUTCDATETIME());
            """;

        var parameters = items.Select(item => new
        {
            SyncRunId = syncRunId,
            item.TargetMailboxUpn,
            item.SourceUserObjectId,
            Action = item.Action.ToString(),
            Result = item.Result.ToString(),
            item.GraphContactId,
            item.ErrorCode,
            item.Message
        });

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task<long> EnsureTargetMailboxAsync(TargetMailbox mailbox, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.TargetMailbox WITH (HOLDLOCK) AS mailboxTarget
            USING
            (
                SELECT
                    @EntraUserObjectId AS EntraUserObjectId,
                    @TargetMailboxUpn AS UserPrincipalName,
                    @TargetMailboxMail AS Mail,
                    @DisplayName AS DisplayName,
                    @FilterSource AS FilterSource
            ) AS mailboxSource
            ON mailboxTarget.UserPrincipalName = mailboxSource.UserPrincipalName
               OR mailboxTarget.Mail = mailboxSource.Mail
            WHEN MATCHED THEN
                UPDATE SET
                    EntraUserObjectId = COALESCE(mailboxSource.EntraUserObjectId, mailboxTarget.EntraUserObjectId),
                    UserPrincipalName = mailboxSource.UserPrincipalName,
                    Mail = mailboxSource.Mail,
                    DisplayName = mailboxSource.DisplayName,
                    FilterSource = mailboxSource.FilterSource,
                    IsEnabled = 1,
                    IsDeleted = 0,
                    UpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (EntraUserObjectId, UserPrincipalName, Mail, DisplayName, FilterSource, IsEnabled, IsDeleted, CreatedUtc, UpdatedUtc)
                VALUES (mailboxSource.EntraUserObjectId, mailboxSource.UserPrincipalName, mailboxSource.Mail,
                        mailboxSource.DisplayName, mailboxSource.FilterSource, 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME());

            SELECT TOP (1) TargetMailboxId
            FROM dbo.TargetMailbox
            WHERE UserPrincipalName = @TargetMailboxUpn OR Mail = @TargetMailboxMail
            ORDER BY TargetMailboxId;
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                TargetMailboxUpn = mailbox.UserPrincipalName,
                TargetMailboxMail = mailbox.Mail,
                mailbox.EntraUserObjectId,
                mailbox.DisplayName,
                FilterSource = mailbox.Source
            },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);

        return await connection.ExecuteScalarAsync<long>(command);
    }

    public async Task BulkUpsertContactSyncStatesAsync(
        long targetMailboxId,
        string targetMailboxUpn,
        IReadOnlyList<ContactSyncStateUpdate> updates,
        CancellationToken cancellationToken)
    {
        if (updates.Count == 0) return;

        const string sql = """
            MERGE dbo.ContactSyncState WITH (HOLDLOCK) AS target
            USING
            (
                SELECT
                    @TargetMailboxId AS TargetMailboxId,
                    @TargetMailboxUpn AS TargetMailboxUpn,
                    @SourceUserObjectId AS SourceUserObjectId,
                    @ExchangeContactId AS ExchangeContactId,
                    @LastFieldHash AS LastFieldHash
            ) AS source
            ON target.TargetMailboxUpn = source.TargetMailboxUpn
               AND target.SourceUserObjectId = source.SourceUserObjectId
            WHEN MATCHED THEN
                UPDATE SET
                    ExchangeContactId = source.ExchangeContactId,
                    LastFieldHash = source.LastFieldHash,
                    IsDeleted = 0,
                    LastSyncedUtc = SYSUTCDATETIME(),
                    UpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT (TargetMailboxId, TargetMailboxUpn, SourceUserObjectId, ExchangeContactId, LastFieldHash, IsDeleted, LastSyncedUtc, CreatedUtc, UpdatedUtc)
                VALUES (source.TargetMailboxId, source.TargetMailboxUpn, source.SourceUserObjectId, source.ExchangeContactId,
                        source.LastFieldHash, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        var parameters = updates.Select(u => new
        {
            TargetMailboxId = targetMailboxId,
            TargetMailboxUpn = targetMailboxUpn,
            SourceUserObjectId = u.SourceUserObjectId,
            ExchangeContactId = u.ExchangeContactId,
            LastFieldHash = u.FieldHash
        });

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task BulkMarkSyncStatesDeletedAsync(
        string targetMailboxUpn,
        IReadOnlyList<Guid> sourceUserObjectIds,
        CancellationToken cancellationToken)
    {
        if (sourceUserObjectIds.Count == 0) return;

        const string sql = """
            UPDATE dbo.ContactSyncState
            SET IsDeleted = 1,
                DeletedUtc = SYSUTCDATETIME(),
                UpdatedUtc = SYSUTCDATETIME()
            WHERE TargetMailboxUpn = @TargetMailboxUpn
              AND SourceUserObjectId = @SourceUserObjectId;
            """;

        var parameters = sourceUserObjectIds.Select(id => new
        {
            TargetMailboxUpn = targetMailboxUpn,
            SourceUserObjectId = id
        });

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken, commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }

    public async Task RecordLegacyFolderCleanupAsync(
        string targetMailboxUpn,
        string legacyFolderName,
        string? legacyFolderId,
        string cleanupStatus,
        CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.MailboxFolderSyncState WITH (HOLDLOCK) AS target
            USING
            (
                SELECT
                    @TargetMailboxUpn AS TargetMailboxUpn,
                    @LegacyFolderName AS LegacyFolderName,
                    @LegacyFolderId AS LegacyFolderId,
                    @LegacyFolderCleanupStatus AS LegacyFolderCleanupStatus
            ) AS source
            ON target.TargetMailboxUpn = source.TargetMailboxUpn
            WHEN MATCHED THEN
                UPDATE SET
                    LegacyFolderName = source.LegacyFolderName,
                    LegacyFolderId = source.LegacyFolderId,
                    LegacyFolderCleanupStatus = source.LegacyFolderCleanupStatus,
                    LegacyFolderCleanupAttemptedUtc = SYSUTCDATETIME(),
                    LegacyFolderCleanedUtc = CASE WHEN source.LegacyFolderCleanupStatus = 'Deleted' THEN SYSUTCDATETIME() ELSE target.LegacyFolderCleanedUtc END,
                    UpdatedUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED BY TARGET THEN
                INSERT
                (
                    TargetMailboxUpn,
                    ManagedFolderName,
                    LegacyFolderName,
                    LegacyFolderId,
                    LegacyFolderCleanupStatus,
                    LegacyFolderCleanupAttemptedUtc,
                    LegacyFolderCleanedUtc,
                    CreatedUtc,
                    UpdatedUtc
                )
                VALUES
                (
                    source.TargetMailboxUpn,
                    N'',
                    source.LegacyFolderName,
                    source.LegacyFolderId,
                    source.LegacyFolderCleanupStatus,
                    SYSUTCDATETIME(),
                    CASE WHEN source.LegacyFolderCleanupStatus = 'Deleted' THEN SYSUTCDATETIME() ELSE NULL END,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                );
            """;

        await using var connection = connectionFactory.CreateConnection();
        var command = new CommandDefinition(
            sql,
            new
            {
                TargetMailboxUpn = targetMailboxUpn,
                LegacyFolderName = legacyFolderName,
                LegacyFolderId = legacyFolderId,
                LegacyFolderCleanupStatus = cleanupStatus
            },
            cancellationToken: cancellationToken,
            commandTimeout: _commandTimeoutSeconds);
        await connection.ExecuteAsync(command);
    }
}
