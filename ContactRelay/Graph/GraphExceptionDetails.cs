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

    /// <summary>
    /// Exchange store error code returned when a folder cannot be deleted, e.g. a default
    /// or otherwise protected contact folder. Such folders can be emptied but never removed.
    /// </summary>
    public const string FolderCannotBeDeletedCode = "ErrorCannotDeleteObject";

    public bool IndicatesFolderCannotBeDeleted =>
        string.Equals(Code, FolderCannotBeDeletedCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Exchange store error code returned when an item no longer exists. A delete that
    /// reports this has already achieved its goal (e.g. the item was removed by a prior,
    /// retried batch attempt), so callers should treat it as success.
    /// </summary>
    public const string ItemNotFoundCode = "ErrorItemNotFound";

    public bool IndicatesItemAlreadyDeleted =>
        StatusCode == 404 ||
        string.Equals(Code, ItemNotFoundCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Graph/Exchange error returned when the target is not a REST/Graph-accessible mailbox
    /// (e.g. an on-premises/hybrid mailbox, an unlicensed user, or a mail user/contact with no
    /// Exchange Online mailbox). Such a target can never be synced, so it should be skipped
    /// rather than treated as a failure.
    /// </summary>
    public const string MailboxNotRestEnabledCode = "MailboxNotEnabledForRESTAPI";

    public bool IndicatesMailboxNotRestEnabled =>
        string.Equals(Code, MailboxNotRestEnabledCode, StringComparison.OrdinalIgnoreCase);

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
