# Deployment

This guide assumes a Windows deployment folder such as `C:\Services\ContactRelay`. Change paths to match your environment.

## Publish

```powershell
dotnet publish .\ContactRelay\ContactRelay.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o C:\Services\ContactRelay
```

Copy a deployment-specific `appsettings.json` into the publish folder, or provide all environment-specific values through environment variables or a secure configuration provider.

## Required Secure Values

- `Graph:TenantId`
- `Graph:ClientId`
- `Graph:ClientSecret` or `Graph:CertificateThumbprint`
- `Sql:ConnectionString`
- `Sentry:Dsn`, only if Sentry is enabled

## Database

```powershell
sqlcmd -S SQL_SERVER_NAME -E -v DatabaseName="CONTACT_RELAY_DATABASE" -i .\sql\001 CreateSchema.sql
```

## Install

Run PowerShell as Administrator:

```powershell
$serviceName = "ContactRelay"
$servicePath = "C:\Services\ContactRelay\ContactRelay.exe"

New-Service `
  -Name $serviceName `
  -BinaryPathName "`"$servicePath`"" `
  -DisplayName $serviceName `
  -Description "Synchronizes directory contacts into Microsoft 365 mailbox contact folders." `
  -StartupType Automatic

Start-Service -Name $serviceName
```

## Uninstall

```powershell
$serviceName = "ContactRelay"
Stop-Service -Name $serviceName -ErrorAction SilentlyContinue
sc.exe delete $serviceName
```

## Pilot

1. Start with `Sync:DryRun=true`.
2. Review `dbo.SyncRun` and `dbo.SyncRunItem`.
3. Confirm the mailbox scope and planned actions.
4. Enable writes only for a small pilot group.
5. Expand gradually after successful live runs.

## Troubleshooting

- Check Windows Event Log and console output for startup validation errors.
- Confirm admin consent and mailbox access scope for Graph failures.
- Confirm SQL connectivity and schema deployment for repository failures.
- Reduce concurrency settings if Microsoft Graph throttling is frequent.
