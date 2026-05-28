using ContactRelay.Models;

namespace ContactRelay.Services;

public interface ISyncOrchestrator
{
    Task<SyncRunSummary> RunAsync(CancellationToken cancellationToken);
}
