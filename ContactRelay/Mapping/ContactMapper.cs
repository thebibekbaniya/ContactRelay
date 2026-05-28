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
            PersonalNotes = BuildManagedNotes(source, options)
        };

        return contact;
    }

    public ManagedMailboxContact ToManagedContact(Contact contact, string managedCategory)
    {
        var sourceUserObjectId = ExtractGuid(contact.PersonalNotes, SourcePrefix);
        var fieldHash = ExtractValue(contact.PersonalNotes, HashPrefix);
        var hasCategory = contact.Categories?.Any(category => string.Equals(category, managedCategory, StringComparison.OrdinalIgnoreCase)) == true;
        var isManaged = hasCategory || sourceUserObjectId.HasValue;

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
                MobilePhone = NormalizePhone(contact.MobilePhone),
                DeskPhone = NormalizePhone(contact.BusinessPhones?.FirstOrDefault(phone => !string.IsNullOrWhiteSpace(phone))),
                Email = NormalizeEmail(contact.EmailAddresses?.FirstOrDefault()?.Address)
            }),
            HasPersonalNotes = HasUserAuthoredNotes(contact.PersonalNotes),
            IsManaged = isManaged
        };
    }

    private static string BuildManagedNotes(DirectoryContact source, SyncWorkerOptions options)
    {
        return string.Join(
            Environment.NewLine,
            options.ManagedByMarker,
            $"{SourcePrefix}{source.SourceUserObjectId}",
            $"{HashPrefix}{source.FieldHash}");
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

    private static bool HasUserAuthoredNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return false;
        }

        return notes
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => !line.StartsWith("ManagedBy=", StringComparison.OrdinalIgnoreCase)
                         && !line.StartsWith(SourcePrefix, StringComparison.OrdinalIgnoreCase)
                         && !line.StartsWith(HashPrefix, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
