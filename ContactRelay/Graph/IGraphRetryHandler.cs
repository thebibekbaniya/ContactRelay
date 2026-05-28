namespace ContactRelay.Graph;

public interface IGraphRetryHandler
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
}
