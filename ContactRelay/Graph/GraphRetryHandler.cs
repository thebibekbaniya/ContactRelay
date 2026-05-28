using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;
using ContactRelay.Options;

namespace ContactRelay.Graph;

public sealed class GraphRetryHandler(IOptions<GraphOptions> options, ILogger<GraphRetryHandler> logger) : IGraphRetryHandler
{
    private readonly GraphOptions _options = options.Value;

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (ApiException ex) when (IsRetryable(ex) && attempt < _options.MaxRetryAttempts)
            {
                var delay = GetDelay(ex, attempt);
                var graphError = GraphExceptionDetails.From(ex);
                logger.LogWarning(
                    "Microsoft Graph request throttled or transiently failed. Attempt={Attempt} Delay={Delay} ErrorType={ErrorType} GraphError={GraphError}",
                    attempt,
                    delay,
                    ex.GetType().Name,
                    graphError.ToSummary());
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async ct =>
            {
                await operation(ct);
                return true;
            },
            cancellationToken);
    }

    private static bool IsRetryable(ApiException ex)
    {
        return ex.ResponseStatusCode is 429 or 500 or 502 or 503 or 504;
    }

    private TimeSpan GetDelay(ApiException ex, int attempt)
    {
        if (ex.ResponseHeaders is not null &&
            ex.ResponseHeaders.TryGetValue("Retry-After", out var retryAfterValues) &&
            retryAfterValues.FirstOrDefault() is { } retryAfter &&
            int.TryParse(retryAfter, out var retryAfterSeconds))
        {
            return TimeSpan.FromSeconds(Math.Clamp(retryAfterSeconds, 1, 3600));
        }

        var exponentialSeconds = Math.Pow(2, attempt - 1) * Math.Max(1, _options.BaseRetryDelaySeconds);
        var jitterMilliseconds = Random.Shared.Next(100, 900);
        return TimeSpan.FromSeconds(Math.Min(exponentialSeconds, 120)) + TimeSpan.FromMilliseconds(jitterMilliseconds);
    }
}
