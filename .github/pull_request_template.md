## Summary

Describe the change and why it is needed.

## Validation

- [ ] `dotnet restore .\ContactRelay.slnx`
- [ ] `dotnet build .\ContactRelay.slnx -c Release`
- [ ] `dotnet test .\ContactRelay.slnx -c Release`
- [ ] `dotnet format .\ContactRelay.slnx --verify-no-changes --no-restore`
- [ ] Dependency vulnerability check completed

## Security and Public-release Checklist

- [ ] No real tenant IDs, client IDs, secrets, domains, mailbox addresses, URLs, certificate identifiers, logs, screenshots, or proprietary values are included.
- [ ] `appsettings.json` and examples contain placeholders or safe defaults only.
- [ ] New logs avoid tokens, authorization headers, mailbox contents, and unnecessary identifiers.
- [ ] Dry-run behavior remains safe.
- [ ] Destructive behavior is documented and disabled by default.
- [ ] Tests or documentation were updated for behavior changes.
