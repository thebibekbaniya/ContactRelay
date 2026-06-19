using System.Text.RegularExpressions;
using Microsoft.Graph.Models;
using ContactRelay.Models;
using ContactRelay.Options;
using ContactRelay.Utilities;

namespace ContactRelay.Mapping;

public sealed partial class ContactMapper : IContactMapper
{
    private const string SourcePrefix = "SourceUserObjectId=";
    private const string HashPrefix = "FieldHash=";

    public DirectoryContact MapUser(User user, string? managerDisplayName, SyncWorkerOptions options)
    {
        if (!Guid.TryParse(user.Id, out var sourceUserObjectId))
        {
            throw new InvalidOperationException("Graph user does not have a GUID object id.");
        }

        var mail = FirstNonEmpty(user.Mail, user.UserPrincipalName)
            ?? throw new InvalidOperationException("Graph user has no mail or UPN value.");

        var deskPhone = user.BusinessPhones?.FirstOrDefault(phone => !string.IsNullOrWhiteSpace(phone));
        var employeeNumber = ReadAdditionalDataString(user, "employeeId")
            ?? ReadAdditionalDataString(user, "employeeNumber");

        var contact = new DirectoryContact
        {
            SourceUserObjectId = sourceUserObjectId,
            UserPrincipalName = Normalize(user.UserPrincipalName),
            Email = NormalizeEmail(mail)!,
            FirstName = Normalize(user.GivenName),
            LastName = Normalize(user.Surname),
            DisplayName = Normalize(user.DisplayName),
            JobTitle = Normalize(user.JobTitle),
            Department = Normalize(user.Department),
            CompanyName = options.CompanyName,
            MobilePhone = NormalizePhone(user.MobilePhone),
            DeskPhone = NormalizePhone(deskPhone),
            Manager = Normalize(managerDisplayName),
            EmployeeNumber = Normalize(employeeNumber),
            AccountEnabled = user.AccountEnabled ?? false
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
                contact.Manager,
                contact.MobilePhone,
                contact.DeskPhone,
                contact.Email
            })
        };
    }

    public Contact ToGraphContact(DirectoryContact source, SyncWorkerOptions options)
    {
        var contact = new Contact
        {
            GivenName = source.FirstName,
            Surname = source.LastName,
            DisplayName = source.DisplayName ?? $"{source.FirstName} {source.LastName}".Trim(),
            JobTitle = source.JobTitle,
            Department = source.Department,
            CompanyName = options.CompanyName,
            Manager = source.Manager,
            MobilePhone = source.MobilePhone,
            BusinessPhones = string.IsNullOrWhiteSpace(source.DeskPhone) ? [] : [source.DeskPhone],
            EmailAddresses =
            [
                new EmailAddress
                {
                    Address = source.Email,
                    Name = source.DisplayName ?? source.Email
                }
            ],
            Categories = [options.ManagedCategory],
            // Sync metadata lives in hidden extended properties, not the user-visible notes
            // field. Clearing notes removes the legacy metadata from contacts created by
            // earlier versions when they are next updated.
            PersonalNotes = "",
            SingleValueExtendedProperties = BuildMetadataProperties(source, options)
        };

        return contact;
    }

    public ManagedMailboxContact ToManagedContact(Contact contact, string managedCategory)
    {
        // Read from the hidden extended properties first, falling back to the legacy notes
        // metadata so contacts created by earlier versions still match until updated.
        var sourceUserObjectId = ReadExtendedGuid(contact, ContactMetadata.SourceUserObjectIdPropertyId)
                                 ?? ExtractGuid(contact.PersonalNotes, SourcePrefix);
        var fieldHash = ReadExtendedProperty(contact, ContactMetadata.FieldHashPropertyId)
                        ?? ExtractValue(contact.PersonalNotes, HashPrefix);
        var hasMarker = ReadExtendedProperty(contact, ContactMetadata.ManagedMarkerPropertyId) is not null
                        || ContainsLegacyMetadata(contact.PersonalNotes);
        var hasCategory = contact.Categories?.Any(category => string.Equals(category, managedCategory, StringComparison.OrdinalIgnoreCase)) == true;
        var isManaged = hasCategory || sourceUserObjectId.HasValue || hasMarker;

        return new ManagedMailboxContact
        {
            ContactId = contact.Id ?? "",
            SourceUserObjectId = sourceUserObjectId,
            Email = contact.EmailAddresses?.FirstOrDefault()?.Address,
            FieldHash = fieldHash,
            ActualFieldHash = HashUtility.ComputeSha256(new
            {
                FirstName = Normalize(contact.GivenName),
                LastName = Normalize(contact.Surname),
                DisplayName = Normalize(contact.DisplayName),
                JobTitle = Normalize(contact.JobTitle),
                Department = Normalize(contact.Department),
                CompanyName = Normalize(contact.CompanyName),
                Manager = Normalize(contact.Manager),
                MobilePhone = NormalizePhone(contact.MobilePhone),
                DeskPhone = NormalizePhone(contact.BusinessPhones?.FirstOrDefault(phone => !string.IsNullOrWhiteSpace(phone))),
                Email = NormalizeEmail(contact.EmailAddresses?.FirstOrDefault()?.Address)
            }),
            HasPersonalNotes = !string.IsNullOrWhiteSpace(contact.PersonalNotes),
            IsManaged = isManaged
        };
    }

    private static List<SingleValueLegacyExtendedProperty> BuildMetadataProperties(DirectoryContact source, SyncWorkerOptions options)
    {
        return
        [
            new SingleValueLegacyExtendedProperty
            {
                Id = ContactMetadata.ManagedMarkerPropertyId,
                Value = options.ManagedByMarker
            },
            new SingleValueLegacyExtendedProperty
            {
                Id = ContactMetadata.SourceUserObjectIdPropertyId,
                Value = source.SourceUserObjectId.ToString()
            },
            new SingleValueLegacyExtendedProperty
            {
                Id = ContactMetadata.FieldHashPropertyId,
                Value = source.FieldHash
            }
        ];
    }

    private static string? ReadExtendedProperty(Contact contact, string propertyId)
    {
        var value = contact.SingleValueExtendedProperties?
            .FirstOrDefault(p => string.Equals(p.Id, propertyId, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Guid? ReadExtendedGuid(Contact contact, string propertyId)
    {
        return Guid.TryParse(ReadExtendedProperty(contact, propertyId), out var guid) ? guid : null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return WhitespaceRegex().Replace(value.Trim(), " ");
    }

    private static string? NormalizeEmail(string? value)
    {
        return Normalize(value)?.ToLowerInvariant();
    }

    private static string? NormalizePhone(string? value)
    {
        return Normalize(value);
    }

    private static string? ReadAdditionalDataString(User user, string key)
    {
        return user.AdditionalData is not null && user.AdditionalData.TryGetValue(key, out var value)
            ? value?.ToString()
            : null;
    }

    private static Guid? ExtractGuid(string? notes, string prefix)
    {
        var value = ExtractValue(notes, prefix);
        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static string? ExtractValue(string? notes, string prefix)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        var line = notes
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return line is null ? null : line[prefix.Length..].Trim();
    }

    private static bool ContainsLegacyMetadata(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        return notes
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.StartsWith("ManagedBy=", StringComparison.OrdinalIgnoreCase)
                         || line.StartsWith(SourcePrefix, StringComparison.OrdinalIgnoreCase)
                         || line.StartsWith(HashPrefix, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
