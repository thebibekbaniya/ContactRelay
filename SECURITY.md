# Security Policy

## Supported Versions

Security fixes are considered for the latest commit on the default branch. If versioned releases are published later, supported release lines should be documented here before distribution.

| Version | Supported |
| --- | --- |
| Default branch | Yes |
| Older snapshots | No |

## Reporting a Vulnerability

Do not open public GitHub issues for suspected vulnerabilities, exposed secrets, tenant-specific configuration, or private operational data.

Report privately using GitHub private vulnerability reporting if it is enabled for the repository. If it is not enabled, contact the maintainers through a private channel designated by the project owner before public disclosure.

Please include:

- A concise description of the issue.
- Affected files, versions, or configuration areas.
- Reproduction steps that use placeholders only.
- Impact and suggested remediation, if known.

## Responsible Disclosure

Give maintainers a reasonable opportunity to investigate and remediate before public disclosure. Avoid accessing, modifying, or exfiltrating data that is not yours. Do not include real tenant IDs, client IDs, mailbox addresses, domains, access tokens, certificates, logs, screenshots, or exported directory data in reports.

## Secret Handling

- Never commit real credentials, tenant IDs, client IDs, certificate thumbprints, domains, mailbox addresses, internal URLs, or organization-specific values.
- Use environment variables, .NET user secrets for local development, Azure Key Vault, managed secret stores, or another approved secure provider.
- Keep `Sync:DryRun=true` until mailbox scope and planned writes are reviewed.
- Keep destructive settings disabled until approved.
- Treat SQL audit tables and operational logs as sensitive because they can contain mailbox and contact identifiers.

## Exposed Credentials

If a real credential, app registration value, connection string, certificate identifier, token, or tenant-specific config may have been committed, staged, copied to a build artifact, or pushed to any remote:

1. Rotate or revoke the credential immediately.
2. Audit all branches, tags, pull requests, forks, releases, packages, logs, and CI artifacts.
3. Remove the value from current files.
4. Rewrite Git history before public release if the value was committed.
5. Notify affected administrators and follow your incident response process.

See [Git history audit guidance](docs/GitHistoryAudit.md) for repository-cleaning options.
