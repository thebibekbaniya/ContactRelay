# Contributing

Thank you for helping improve ContactRelay.

## Ground Rules

- Keep the project generic and suitable for public distribution.
- Do not include real tenant IDs, client IDs, domains, mailbox addresses, URLs, user names, group names, certificate thumbprints, secrets, logs, screenshots, or exported directory data.
- Use placeholder values such as `TENANT_ID`, `CLIENT_ID`, `SQL_CONNECTION_STRING`, and `mailbox@example.test` in examples and tests.
- Preserve dry-run safety and least-privilege guidance.

## Development Setup

```powershell
dotnet restore .\ContactRelay.slnx
dotnet build .\ContactRelay.slnx -c Release
dotnet test .\ContactRelay.slnx -c Release
```

For local runs, keep real settings out of source control. Use .NET user secrets, environment variables, or another secure provider.

## Pull Requests

Before opening a pull request:

- Run restore, build, tests, and formatting checks.
- Add or update tests for behavior changes.
- Update README or docs for user-visible configuration or deployment changes.
- Confirm `appsettings.json`, `appsettings.example.json`, scripts, docs, and tests contain placeholders only.
- Confirm no generated `bin`, `obj`, publish output, logs, test results, coverage files, databases, certificates, or local settings are included.

## Security-sensitive Changes

Security, authentication, authorization, logging, data retention, and Graph permission changes should include a short threat/risk note in the pull request. If a change may expose or alter mailbox data, explain the expected behavior in dry-run mode and live mode.
