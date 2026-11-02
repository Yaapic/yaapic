using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestListItemTests
{
    [Theory]
    [InlineData(HttpMethodKind.Put, "PUT")]
    [InlineData(HttpMethodKind.Patch, "PATCH")]
    [InlineData(HttpMethodKind.Delete, "DELETE")]
    [InlineData(HttpMethodKind.Head, "HEAD")]
    [InlineData(HttpMethodKind.Options, "OPTIONS")]
    public void MethodTextNamesEveryMethod(HttpMethodKind method, string expected) =>
        Assert.Equal(expected, new RequestListItem(Guid.NewGuid(), "r", method, "").MethodText);
}
