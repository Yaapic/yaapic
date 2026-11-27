using Yaapic.Core.Mocks;
using Yaapic.Domain.Mocks;

namespace Yaapic.Core.Tests.Mocks;

public class MockRouteMatcherTests
{
    private static MockRoute R(string path, string method = "GET", bool enabled = true) =>
        MockRoute.Create(path) with { Path = path, Method = method, Enabled = enabled };

    private static MockMatch? M(string method, string path, params MockRoute[] routes) =>
        MockRouteMatcher.Match(routes, method, path);

    [Fact]
    public void Parse_splits_target_and_decodes()
    {
        var r = MockHttpRequest.Parse("GET", "/a%20b/%C3%A9?x=1&y=a+b&z=%41&flag", [], [], "HTTP/1.1");
        Assert.Equal("/a b/é", r.Path);
        Assert.Equal("x=1&y=a+b&z=%41&flag", r.Query);
        Assert.Equal("/a%20b/%C3%A9?x=1&y=a+b&z=%41&flag", r.RawTarget);
        Assert.Equal(["x", "y", "z", "flag"], r.QueryValues.Select(q => q.Key));
        Assert.Equal(["1", "a b", "A", ""], r.QueryValues.Select(q => q.Value));
    }

    [Fact]
    public void Parse_keeps_plus_in_path_and_invalid_percent()
    {
        var r = MockHttpRequest.Parse("GET", "/a+b/%zz/%FF/100%", [], [], "HTTP/1.1");
        Assert.Equal("/a+b/%zz/%FF/100%", r.Path);
    }

    [Fact]
    public void Parse_absolute_form_keeps_only_the_path()
    {
        var r = MockHttpRequest.Parse("GET", "http://example.com:8080/users/1?x=2", [], [], "HTTP/1.1");
        Assert.Equal("/users/1", r.Path);
        Assert.Equal("x=2", r.Query);
        Assert.Equal("/", MockHttpRequest.Parse("GET", "http://example.com", [], [], "HTTP/1.1").Path);
    }

    [Fact]
    public void Header_is_case_insensitive()
    {
        var r = MockHttpRequest.Parse("GET", "/", [new("X-A", "1"), new("x-a", "2")], [], "HTTP/1.1");
        Assert.Equal("1", r.Header("x-A"));
        Assert.Null(r.Header("nope"));
    }

    [Fact]
    public void Literal_match_is_case_sensitive()
    {
        Assert.NotNull(M("GET", "/users", R("/users")));
        Assert.Null(M("GET", "/Users", R("/users")));
    }

    [Theory]
    [InlineData("/users/:id")]
    [InlineData("/users/{id}")]
    public void Named_segment_is_captured(string template)
    {
        var m = M("GET", "/users/42", R(template));
        Assert.Equal("42", m!.Params["id"]);
        Assert.Null(M("GET", "/users", R(template)));
        Assert.Null(M("GET", "/users//", R(template)));
        Assert.Null(M("GET", "/users/1/x", R(template)));
    }

    [Fact]
    public void Star_matches_the_rest()
    {
        Assert.Equal("a/b/c", M("GET", "/a/b/c", R("/*"))!.Params["*"]);
        Assert.Equal("", M("GET", "/", R("/*"))!.Params["*"]);
        Assert.Equal("x/y", M("GET", "/api/x/y", R("/api/*"))!.Params["*"]);
        Assert.Equal("", M("GET", "/api", R("/api/*"))!.Params["*"]);
        Assert.Null(M("GET", "/other/x", R("/api/*")));
    }

    [Fact]
    public void Trailing_slash_is_ignored_but_not_root()
    {
        Assert.NotNull(M("GET", "/a/", R("/a")));
        Assert.NotNull(M("GET", "/a", R("/a/")));
        Assert.NotNull(M("GET", "/", R("/")));
        Assert.Null(M("GET", "/a", R("/")));
    }

    [Fact]
    public void Empty_segments_are_literals()
    {
        Assert.Null(M("GET", "/a//b", R("/a/b")));
        Assert.NotNull(M("GET", "/a//b", R("/a//b")));
    }

    [Fact]
    public void Method_rules()
    {
        Assert.NotNull(M("DELETE", "/a", R("/a", "ANY")));
        Assert.Null(M("POST", "/a", R("/a")));
        Assert.NotNull(M("HEAD", "/a", R("/a")));
        Assert.Null(M("GET", "/a", R("/a", "HEAD")));
        Assert.NotNull(M("get", "/a", R("/a")));
    }

    [Fact]
    public void Disabled_routes_are_skipped_and_first_match_wins()
    {
        var general = R("/users/:id");
        var specific = R("/users/me");
        Assert.Same(general, M("GET", "/users/me", R("/users/me", enabled: false), general, specific)!.Route);
        Assert.Null(M("GET", "/zzz", general));
    }
}
