using ContactRelay.Graph;
using Microsoft.Graph.Models.ODataErrors;

namespace ContactRelay.Tests;

public sealed class GraphExceptionDetailsTests
{
    [Fact]
    public void IndicatesItemAlreadyDeleted_TrueForErrorItemNotFoundCode()
    {
        var details = GraphExceptionDetails.From(new ODataError
        {
            ResponseStatusCode = 404,
            Error = new MainError { Code = GraphExceptionDetails.ItemNotFoundCode, Message = "not found" }
        });

        Assert.True(details.IndicatesItemAlreadyDeleted);
    }

    [Fact]
    public void IndicatesItemAlreadyDeleted_TrueFor404WithoutCode()
    {
        var details = GraphExceptionDetails.From(new ApiExceptionStub(404));

        Assert.True(details.IndicatesItemAlreadyDeleted);
    }

    [Fact]
    public void IndicatesItemAlreadyDeleted_FalseForOtherErrors()
    {
        var details = GraphExceptionDetails.From(new ODataError
        {
            ResponseStatusCode = 403,
            Error = new MainError { Code = GraphExceptionDetails.FolderCannotBeDeletedCode, Message = "cannot delete" }
        });

        Assert.False(details.IndicatesItemAlreadyDeleted);
        Assert.True(details.IndicatesFolderCannotBeDeleted);
    }

    private sealed class ApiExceptionStub : Microsoft.Kiota.Abstractions.ApiException
    {
        public ApiExceptionStub(int statusCode)
            : base("Graph failure")
        {
            ResponseStatusCode = statusCode;
        }
    }
}
