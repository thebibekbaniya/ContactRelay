namespace ContactRelay.Models;

public sealed class SyncRunSummary
{
    public long SyncRunId { get; init; }

    public int TargetMailboxCount { get; set; }

    public int PublishedContactCount { get; set; }

    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int DeletedCount { get; set; }

    public int SkippedCount { get; set; }

    public int ErrorCount { get; set; }
}
