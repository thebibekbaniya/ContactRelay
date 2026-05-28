namespace ContactRelay.Models;

public sealed class SyncExclusion
{
    public long SyncExclusionId { get; init; }

    public string ExclusionType { get; init; } = "";

    public string ExclusionValue { get; init; } = "";

    public bool IsEnabled { get; init; } = true;
}
