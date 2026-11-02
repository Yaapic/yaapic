using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class HttpMethodKindExtensionsTests
{
    [Theory]
    [InlineData(HttpMethodKind.Get, "GET", false)]
    [InlineData(HttpMethodKind.Post, "POST", true)]
    [InlineData(HttpMethodKind.Put, "PUT", true)]
    [InlineData(HttpMethodKind.Patch, "PATCH", true)]
    [InlineData(HttpMethodKind.Delete, "DELETE", true)]
    [InlineData(HttpMethodKind.Head, "HEAD", false)]
    [InlineData(HttpMethodKind.Options, "OPTIONS", false)]
    public void ToText_TryParse_round_trip_and_AllowsBody(HttpMethodKind method, string text, bool allowsBody)
    {
        Assert.Equal(text, method.ToText());
        Assert.True(HttpMethodKindExtensions.TryParse(text, out var parsed));
        Assert.Equal(method, parsed);
        Assert.Equal(allowsBody, method.AllowsBody());
    }

    [Theory]
    [InlineData("get")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("TRACE")]
    public void TryParse_rejects_unknown_text(string? text) =>
        Assert.False(HttpMethodKindExtensions.TryParse(text, out _));
}
