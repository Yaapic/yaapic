using System.Globalization;
using System.Text;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;

namespace Yaapic.Core.Mocks;

/// <summary>The response to a mock request.</summary>
/// <param name="Response">The response to send.</param>
/// <param name="RouteName">The matched route name; <see langword="null"/> for a preflight or no match.</param>
public sealed record MockResult(MockHttpResponse Response, string? RouteName);

/// <summary>Turns a request into the response of a mock server (ADR 0020).</summary>
public static class MockResponder
{
    private const string AllowOrigin = "Access-Control-Allow-Origin";
    private const string NoMatch = "No mock route matches";

    /// <summary>Answers a request: preflight, matching route or 404.</summary>
    /// <param name="server">The server definition.</param>
    /// <param name="request">The request.</param>
    /// <returns>The response and the matched route name.</returns>
    public static MockResult Respond(MockServerDefinition server, MockHttpRequest request)
    {
        var match = MockRouteMatcher.Match(server.Routes, request.Method, request.Path);
        if (match is null && server.Cors && IsPreflight(request))
        {
            return new MockResult(Preflight(request), null);
        }

        var head = request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase);
        MockHttpResponse response;
        string? name = null;
        if (match is null)
        {
            var body = "{\"error\":\"" + NoMatch + " " + Escape(request.Method + " " + request.Path) + "\"}";
            response = new MockHttpResponse(
                404, ReasonPhrases.For(404), [new("Content-Type", "application/json")],
                Encoding.UTF8.GetBytes(body), TimeSpan.Zero, head);
        }
        else
        {
            var route = match.Route;
            name = route.Name;
            List<KeyValueEntry> headers = [];
            foreach (var h in route.Headers.Where(h => h.Enabled))
            {
                headers.Add(new KeyValueEntry(h.Key, MockTemplate.Render(h.Value, request, match.Params)));
            }

            response = new MockHttpResponse(
                route.Status, ReasonPhrases.For(route.Status), headers,
                Encoding.UTF8.GetBytes(MockTemplate.Render(route.Body, request, match.Params)),
                TimeSpan.FromMilliseconds(route.DelayMs), head);
        }

        if (server.Cors && !response.Headers.Any(h => h.Key.Equals(AllowOrigin, StringComparison.OrdinalIgnoreCase)))
        {
            response = response with { Headers = [.. response.Headers, new KeyValueEntry(AllowOrigin, "*")] };
        }

        return new MockResult(response, name);
    }

    private static bool IsPreflight(MockHttpRequest request) =>
        request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase)
        && request.Header("Access-Control-Request-Method") is not null;

    private static MockHttpResponse Preflight(MockHttpRequest request) =>
        new(204, ReasonPhrases.For(204),
        [
            new(AllowOrigin, "*"),
            new("Access-Control-Allow-Methods", "GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS"),
            new("Access-Control-Allow-Headers", request.Header("Access-Control-Request-Headers") ?? "*"),
            new("Access-Control-Max-Age", "600"),
        ],
        [], TimeSpan.Zero);

    private static string Escape(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case < ' ':
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        return sb.ToString();
    }
}
