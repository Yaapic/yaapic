using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class HttpRequestDefinitionTests
{
    [Fact]
    public void Create_trims_name()
    {
        var r = HttpRequestDefinition.Create("  Login ");
        Assert.Equal("Login", r.Name);
        Assert.Equal(HttpMethodKind.Get, r.Method);
        Assert.Equal(RequestBody.None, r.Body);
        Assert.Equal(RequestAuth.None, r.Auth);
        Assert.Equal(RequestSettings.Default, r.Settings);
    }

    [Fact]
    public void Negative_timeout_throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new RequestSettings(true, -1));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_rejects_blank_name(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => HttpRequestDefinition.Create(name!));

    [Fact]
    public void Create_gives_distinct_ids() =>
        Assert.NotEqual(HttpRequestDefinition.Create("a").Id, HttpRequestDefinition.Create("a").Id);

    static HttpRequestDefinition Sample() => HttpRequestDefinition.Create("a") with
    {
        Url = "http://x",
        QueryParams = [new("q", "1")],
        Headers = [new("h", "1")],
        Body = RequestBody.Raw(RawContentType.Json, "{}"),
    };

    [Fact]
    public void IsEquivalentTo_true_for_equal_content_in_different_lists()
    {
        var a = Sample();
        var b = a with { QueryParams = [new("q", "1")], Headers = [new("h", "1")] };
        Assert.True(a.IsEquivalentTo(b));
    }

    public static TheoryData<Func<HttpRequestDefinition, HttpRequestDefinition>> Changes => new()
    {
        r => r with { Name = "b" },
        r => r with { Method = HttpMethodKind.Post },
        r => r with { Url = "http://y" },
        r => r with { QueryParams = [new("q", "2")] },
        r => r with { Headers = [new("h", "2")] },
        r => r with { Body = RequestBody.Raw(RawContentType.Json, "[]") },
        r => r with { Auth = RequestAuth.None with { Kind = AuthKind.Bearer, Token = "t" } },
        r => r with { Settings = new RequestSettings(false, 0) },
        r => r with { Settings = new RequestSettings(true, 5) },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void IsEquivalentTo_false_after_change(Func<HttpRequestDefinition, HttpRequestDefinition> change)
    {
        var a = Sample();
        Assert.False(a.IsEquivalentTo(change(a)));
    }
}
