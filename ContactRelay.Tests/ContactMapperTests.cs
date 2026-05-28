using ContactRelay.Mapping;
using ContactRelay.Models;
using ContactRelay.Options;
using Microsoft.Graph.Models;

namespace ContactRelay.Tests;

public sealed class ContactMapperTests
{
    private readonly ContactMapper _mapper = new();
    private readonly SyncWorkerOptions _options = new()
    {
        ManagedCategory = "ContactRelay Managed",
        ManagedByMarker = "ManagedBy=ContactRelay",
        CompanyName = "Example Organization"
    };

    [Fact]
    public void MapUser_NormalizesFieldsAndComputesHash()
    {
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId.ToString(),
            UserPrincipalName = " PERSON@EXAMPLE.COM ",
            Mail = " PERSON@EXAMPLE.COM ",
            GivenName = " First ",
            Surname = " Last ",
            DisplayName = " First   Last ",
            MobilePhone = " 555-0100 ",
            BusinessPhones = [" 555-0101 "],
            AccountEnabled = true
        };

        var contact = _mapper.MapUser(user, " Manager   Name ", _options);

        Assert.Equal(userId, contact.SourceUserObjectId);
        Assert.Equal("person@example.com", contact.Email);
        Assert.Equal("First Last", contact.DisplayName);
        Assert.Equal("Manager Name", contact.Manager);
        Assert.False(string.IsNullOrWhiteSpace(contact.FieldHash));
    }

    [Fact]
    public void MapUser_Throws_WhenObjectIdIsMissingOrInvalid()
    {
        var user = new User { Id = "not-a-guid", Mail = "person@example.test" };

        var ex = Assert.Throws<InvalidOperationException>(() => _mapper.MapUser(user, null, _options));

        Assert.Equal("Graph user does not have a GUID object id.", ex.Message);
    }

    [Fact]
    public void MapUser_Throws_WhenMailAndUpnAreMissing()
    {
        var user = new User { Id = Guid.NewGuid().ToString() };

        var ex = Assert.Throws<InvalidOperationException>(() => _mapper.MapUser(user, null, _options));

        Assert.Equal("Graph user has no mail or UPN value.", ex.Message);
    }

    [Fact]
    public void ToManagedContact_DetectsNoChangeAgainstMappedContact()
    {
        var source = SourceContact();
        var graphContact = _mapper.ToGraphContact(source, _options);
        graphContact.Id = "contact-id";

        var managed = _mapper.ToManagedContact(graphContact, _options.ManagedCategory);

        Assert.True(managed.IsManaged);
        Assert.Equal(source.SourceUserObjectId, managed.SourceUserObjectId);
        Assert.Equal(source.FieldHash, managed.FieldHash);
        Assert.Equal(source.FieldHash, managed.ActualFieldHash);
        Assert.False(managed.HasPersonalNotes);
    }

    [Fact]
    public void ToManagedContact_FlagsUserAuthoredNotesAsChanged()
    {
        var source = SourceContact();
        var graphContact = _mapper.ToGraphContact(source, _options);
        graphContact.Id = "contact-id";
        graphContact.PersonalNotes += $"{Environment.NewLine}User note";

        var managed = _mapper.ToManagedContact(graphContact, _options.ManagedCategory);

        Assert.True(managed.HasPersonalNotes);
    }

    private static DirectoryContact SourceContact()
    {
        var sourceId = Guid.NewGuid();
        return new DirectoryContact
        {
            SourceUserObjectId = sourceId,
            UserPrincipalName = "person@example.test",
            Email = "person@example.test",
            FirstName = "First",
            LastName = "Last",
            DisplayName = "First Last",
            CompanyName = "Example Organization",
            MobilePhone = "555-0100",
            DeskPhone = "555-0101",
            AccountEnabled = true
        } with
        {
            FieldHash = ContactRelay.Utilities.HashUtility.ComputeSha256(new
            {
                FirstName = "First",
                LastName = "Last",
                DisplayName = "First Last",
                JobTitle = (string?)null,
                Department = (string?)null,
                CompanyName = "Example Organization",
                MobilePhone = "555-0100",
                DeskPhone = "555-0101",
                Email = "person@example.test"
            })
        };
    }
}
