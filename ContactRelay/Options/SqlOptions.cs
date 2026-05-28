using System.ComponentModel.DataAnnotations;

namespace ContactRelay.Options;

public sealed class SqlOptions
{
    public const string SectionName = "Sql";

    [Required]
    public string ConnectionString { get; init; } = "";

    public int CommandTimeoutSeconds { get; init; }
}
