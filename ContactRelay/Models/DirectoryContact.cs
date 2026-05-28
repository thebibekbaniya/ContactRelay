namespace ContactRelay.Models;

public sealed record DirectoryContact
{
    public required Guid SourceUserObjectId { get; init; }

    public string? UserPrincipalName { get; init; }

    public required string Email { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? DisplayName { get; init; }

    public string? JobTitle { get; init; }

    public string? Department { get; init; }

    public string? CompanyName { get; init; }

    public string? MobilePhone { get; init; }

    public string? DeskPhone { get; init; }

    public string? Manager { get; init; }

    public string? EmployeeNumber { get; init; }

    public bool AccountEnabled { get; init; } = true;

    public string FieldHash { get; init; } = "";
}
