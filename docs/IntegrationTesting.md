# Integration Test Plan

Automated unit tests in this repository do not require Microsoft 365, Microsoft Graph, Exchange Online, or SQL Server. Real integration tests must be run only against an approved non-production tenant.

## Safety Requirements

- Never use a production tenant for first validation.
- Never commit tenant IDs, client IDs, mailbox addresses, domains, group names, certificate thumbprints, secrets, logs, or screenshots.
- Keep `Sync:DryRun=true` for initial runs.
- Scope Graph mailbox access to dedicated test mailboxes.
- Use test mailboxes and test source users only.

## Required Environment Variables

Integration runs should be disabled by default and require explicit values:

```powershell
$env:CONTACTRELAY_RUN_INTEGRATION_TESTS = "true"
$env:Graph__TenantId = "TENANT_ID"
$env:Graph__ClientId = "CLIENT_ID"
$env:Graph__ClientSecret = "CLIENT_SECRET"
$env:Sql__ConnectionString = "SQL_CONNECTION_STRING"
$env:Sync__DryRun = "true"
$env:Sync__TargetAllUserMailboxes = "false"
```

Prefer certificate authentication or a managed secret provider for persistent environments.

## Suggested Test Flow

1. Deploy the SQL schema to a non-production SQL database.
2. Create a dedicated app registration and grant only the required Graph application permissions.
3. Scope Exchange mailbox access to dedicated test mailboxes.
4. Create a target mailbox group and source contact group with test objects only.
5. Run with `Sync:DryRun=true` and verify `SyncRun` and `SyncRunItem`.
6. Enable writes for a small test scope.
7. Confirm creates, updates, skips, duplicate cleanup, and out-of-scope handling.
8. Disable destructive cleanup until behavior is validated.
9. Remove all test data after the run.

## CI Behavior

Repository CI should run unit tests only. Integration tests must require explicit environment variables and should never run for untrusted pull requests.
