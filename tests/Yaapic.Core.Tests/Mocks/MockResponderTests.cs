using System.Text;
using Yaapic.Core.Mocks;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;

namespace Yaapic.Core.Tests.Mocks;

public class MockResponderTests
{
    private static MockHttpRequest Req(string method, string target, string body = "", params KeyValueEntry[] headers) =>
        MockHttpRequest.Parse(method, target, headers, Encoding.UTF8.GetBytes(body), "HTTP/1.1");

    private static MockServerDefinition Server(bool cors, params MockRoute[] routes) =>
        MockServerDefinition.Create("s") with { Cors = cors, Routes = routes };

    private static MockRoute Route(string path, string method = "GET", string body = "{}", params KeyValueEntry[] headers) =>
        MockRoute.Create("r") with { Path = path, Method = method, Body = body, Headers = headers };

    private static string Text(MockHttpResponse r) => Encoding.UTF8.GetString(r.Body);

    private static string Header(MockHttpResponse r, string name) =>
        r.Headers.Single(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    [Fact]
    public void Template_replaces_every_token()
    {
        var req = Req("POST", "/u/7?a=1&a=2", "hello", new KeyValueEntry("X-Id", "abc"));
        var p = new Dictionary<string, string> { ["id"] = "7" };
        Assert.Equal("7|1|abc|hello", MockTemplate.Render("{{params.id}}|{{query.a}}|{{headers.x-id}}|{{body}}", req, p));
    }

    [Fact]
    public void Template_missing_names_are_empty_and_unknown_tokens_kept()
    {
        var req = Req("GET", "/");
        Assert.Equal("[][][]", MockTemplate.Render("[{{params.x}}][{{query.x}}][{{headers.x}}]", req, new Dictionary<string, string>()));
        Assert.Equal("{{other}} {{ nope }}", MockTemplate.Render("{{other}} {{ nope }}", req, new Dictionary<string, string>()));
    }

    [Fact]
    public void Template_dynamic_variables_are_fresh_per_occurrence()
    {
        var req = Req("GET", "/");
        var parts = MockTemplate.Render("{{$guid}} {{$guid}}", req, new Dictionary<string, string>()).Split(' ');
        Assert.NotEqual(parts[0], parts[1]);
        Assert.True(long.TryParse(MockTemplate.Render("{{$timestamp}}", req, new Dictionary<string, string>()), out _));
    }

    [Fact]
    public void Matched_route_renders_headers_and_delay_and_skips_disabled()
    {
        var route = Route("/u/:id", "GET", "id={{params.id}}",
            new KeyValueEntry("X-Id", "{{params.id}}"), new KeyValueEntry("X-Off", "no", false)) with
        { Status = 201, DelayMs = 50 };
        var result = MockResponder.Respond(Server(false, route), Req("GET", "/u/9"));
        Assert.Equal("r", result.RouteName);
        Assert.Equal(201, result.Response.Status);
        Assert.Equal("Created", result.Response.Reason);
        Assert.Equal("9", Header(result.Response, "X-Id"));
        Assert.DoesNotContain(result.Response.Headers, h => h.Key == "X-Off");
        Assert.Equal("id=9", Text(result.Response));
        Assert.Equal(TimeSpan.FromMilliseconds(50), result.Response.Delay);
        Assert.False(result.Response.OmitBody);
    }

    [Fact]
    public void Head_omits_the_body()
    {
        var result = MockResponder.Respond(Server(false, Route("/a", "GET", "xyz")), Req("HEAD", "/a"));
        Assert.True(result.Response.OmitBody);
        Assert.Equal("xyz", Text(result.Response));
    }

    [Fact]
    public void No_match_gives_escaped_404()
    {
        var result = MockResponder.Respond(Server(false), Req("GET", "/a\"b\\c"));
        Assert.Null(result.RouteName);
        Assert.Equal(404, result.Response.Status);
        Assert.Equal("application/json", Header(result.Response, "Content-Type"));
        Assert.Equal("{\"error\":\"No mock route matches GET /a\\\"b\\\\c\"}", Text(result.Response));
    }

    [Fact]
    public void Preflight_is_answered_when_cors_is_on()
    {
        var req = Req("OPTIONS", "/x", "", new KeyValueEntry("Access-Control-Request-Method", "POST"), new KeyValueEntry("Access-Control-Request-Headers", "X-A"));
        var result = MockResponder.Respond(Server(true), req);
        Assert.Null(result.RouteName);
        Assert.Equal(204, result.Response.Status);
        Assert.Empty(result.Response.Body);
        Assert.Equal("*", Header(result.Response, "Access-Control-Allow-Origin"));
        Assert.Equal("GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS", Header(result.Response, "Access-Control-Allow-Methods"));
        Assert.Equal("X-A", Header(result.Response, "Access-Control-Allow-Headers"));
        Assert.Equal("600", Header(result.Response, "Access-Control-Max-Age"));
        var star = MockResponder.Respond(Server(true), Req("OPTIONS", "/x", "", new KeyValueEntry("Access-Control-Request-Method", "GET")));
        Assert.Equal("*", Header(star.Response, "Access-Control-Allow-Headers"));
    }

    [Fact]
    public void Preflight_is_not_answered_with_an_options_route_or_cors_off()
    {
        var headers = new KeyValueEntry("Access-Control-Request-Method", "POST");
        var withRoute = MockResponder.Respond(Server(true, Route("/x", "OPTIONS", "o")), Req("OPTIONS", "/x", "", headers));
        Assert.Equal("r", withRoute.RouteName);
        Assert.Equal(200, withRoute.Response.Status);
        var any = MockResponder.Respond(Server(true, Route("/x", "ANY", "o")), Req("OPTIONS", "/x", "", headers));
        Assert.Equal("r", any.RouteName);
        var off = MockResponder.Respond(Server(false), Req("OPTIONS", "/x", "", headers));
        Assert.Equal(404, off.Response.Status);
    }

    [Fact]
    public void Allow_origin_is_added_once()
    {
        var added = MockResponder.Respond(Server(true, Route("/a")), Req("GET", "/a"));
        Assert.Single(added.Response.Headers, h => h.Key == "Access-Control-Allow-Origin");
        var own = Route("/a", "GET", "{}", new KeyValueEntry("access-control-allow-origin", "https://x"));
        var kept = MockResponder.Respond(Server(true, own), Req("GET", "/a"));
        Assert.Equal("https://x", Header(kept.Response, "Access-Control-Allow-Origin"));
        Assert.Single(kept.Response.Headers, h => h.Key.Equals("Access-Control-Allow-Origin", StringComparison.OrdinalIgnoreCase));
        var notFound = MockResponder.Respond(Server(true), Req("GET", "/z"));
        Assert.Equal("*", Header(notFound.Response, "Access-Control-Allow-Origin"));
    }

    [Fact]
    public void Log_entry_truncates_the_preview()
    {
        var req = Req("POST", "/a?b=1", new string('x', 5000), new KeyValueEntry("H", "v"));
        var res = new MockHttpResponse(200, "OK", [], [], TimeSpan.Zero);
        var at = DateTimeOffset.UnixEpoch;
        var e = MockLogEntry.From(req, res, "r", at, TimeSpan.FromMilliseconds(3));
        Assert.Equal(4096, e.RequestBodyPreview.Length);
        Assert.Equal(("POST", "/a?b=1", 200, "r"), (e.Method, e.Target, e.Status, e.RouteName));
        Assert.Equal(at, e.At);
        Assert.Single(e.RequestHeaders);
        Assert.Equal("ab", MockLogEntry.From(Req("POST", "/", "ab"), res, null, at, TimeSpan.Zero).RequestBodyPreview);
    }
}
