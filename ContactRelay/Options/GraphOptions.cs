using System.ComponentModel.DataAnnotations;

namespace ContactRelay.Options;

public sealed class GraphOptions
{
    public const string SectionName = "Graph";

    [Required]
    public string TenantId { get; set; } = "";

    [Required]
    public string ClientId { get; set; } = "";

    public string? ClientSecret { get; set; }

    public string? CertificateThumbprint { get; set; }

    public string CertificateStoreLocation { get; set; } = "";

    public string[] Scopes { get; set; } = [];

    public int PageSize { get; set; }

    public int BatchSize { get; set; }

    public int MaxRetryAttempts { get; set; }

    public int BaseRetryDelaySeconds { get; set; }

    public int UserLookupConcurrency { get; set; }

    public string? TargetMailboxSecurityGroupId { get; set; }

    public string? PublishedContactSecurityGroupId { get; set; }
}
