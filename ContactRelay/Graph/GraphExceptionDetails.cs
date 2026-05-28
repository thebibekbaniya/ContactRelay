using Microsoft.Graph.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;

namespace ContactRelay.Graph;

public sealed record GraphExceptionDetails(
    int? StatusCode,
    string? Code,
    string? Message,
    string? Target,
    string? RequestId,
    string? ClientRequestId)
{
    public static GraphExceptionDetails From(Exception exception)
    {
        var apiException = exception as ApiException;
        var odataError = exception as ODataError;
        var error = odataError?.Error;
        var innerError = error?.InnerError;

        return new GraphExceptionDetails(
            apiException?.ResponseStatusCode,
            error?.Code,
            error?.Message ?? exception.Message,
            error?.Target,
            innerError?.RequestId ?? GetHeaderValue(apiException, "request-id"),
            innerError?.ClientRequestId ?? GetHeaderValue(apiException, "client-request-id"));
    }

    public string ToSummary()
    {
        var parts = new List<string>();
        AddPart(parts, "Status", StatusCode?.ToString());
        AddPart(parts, "Code", Code);
        AddPart(parts, "RequestId", RequestId);
        AddPart(parts, "ClientRequestId", ClientRequestId);

        return parts.Count == 0
            ? "No Microsoft Graph error details were available."
            : string.Join("; ", parts);
    }

    private static void AddPart(ICollection<string> parts, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parts.Add($"{name}={value}");
        }
    }

    private static string? GetHeaderValue(ApiException? exception, string headerName)
    {
        if (exception?.ResponseHeaders is null ||
            !exception.ResponseHeaders.TryGetValue(headerName, out var values))
        {
            return null;
        }

        return values.FirstOrDefault();
    }
}
