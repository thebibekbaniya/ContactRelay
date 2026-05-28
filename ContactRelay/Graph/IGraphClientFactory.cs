using Microsoft.Graph;

namespace ContactRelay.Graph;

public interface IGraphClientFactory
{
    GraphServiceClient CreateClient();
}
