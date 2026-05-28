using ContactRelay.Graph;
using ContactRelay.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Kiota.Abstractions;

namespace ContactRelay.Tests;

public sealed class GraphRetryHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_RetriesTransientGraphFailures()
    {
        var attempts = 0;
        var handler = new GraphRetryHandler(
            Microsoft.Extensions.Options.Options.Create(new GraphOptions { MaxRetryAttempts = 2, BaseRetryDelaySeconds = 1 }),
            NullLogger<GraphRetryHandler>.Instance);

        var result = await handler.ExecuteAsync<string>(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw new TestApiException(429);
            }

            return Task.FromResult("ok");
        }, CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetryNonTransientFailures()
    {
        var attempts = 0;
        var handler = new GraphRetryHandler(
            Microsoft.Extensions.Options.Options.Create(new GraphOptions { MaxRetryAttempts = 3, BaseRetryDelaySeconds = 1 }),
            NullLogger<GraphRetryHandler>.Instance);

        await Assert.ThrowsAsync<TestApiException>(() => handler.ExecuteAsync<string>(_ =>
        {
            attempts++;
            throw new TestApiException(400);
        }, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    private sealed class TestApiException : ApiException
    {
        public TestApiException(int statusCode)
            : base("Graph failure")
        {
            ResponseStatusCode = statusCode;
        }
    }
}
