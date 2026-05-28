# Microsoft Graph Permissions

Use Microsoft Graph application permissions with admin consent.

Required permissions:

- `User.Read.All`
- `Directory.Read.All`
- `Contacts.ReadWrite`

Recommended when mailbox-purpose exclusions are enabled:

- `MailboxSettings.Read`

`Contacts.ReadWrite` can create, read, update, and delete contacts in allowed mailboxes without a signed-in user. Restrict mailbox access with Exchange Online application RBAC, or with application access policies where your tenant still uses them.

## App Registration Checklist

1. Create a Microsoft Entra app registration.
2. Add the Microsoft Graph application permissions above.
3. Grant tenant-wide admin consent.
4. Add a certificate credential or a client secret.
5. Store the credential outside source control.
6. Configure `Graph:TenantId`, `Graph:ClientId`, and either `Graph:CertificateThumbprint` or `Graph:ClientSecret`.

## Mailbox Scope

Application permissions can reach all mailboxes allowed by Exchange Online. Scope access to approved mailboxes before production rollout. Test the app against a pilot mailbox group before expanding scope.

Use placeholder values in examples and scripts. Never commit real tenant IDs, client IDs, group addresses, mailbox addresses, or credentials.
