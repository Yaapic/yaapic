using Yaapic.Domain.Mocks;

namespace Yaapic.Core.Mocks;

/// <summary>Finds the mock route answering a request.</summary>
public static class MockRouteMatcher
{
    /// <summary>Returns the first enabled route matching the method and the decoded path.</summary>
    /// <param name="routes">The routes, in priority order.</param>
    /// <param name="method">The request method.</param>
    /// <param name="path">The percent-decoded request path, without the query.</param>
    /// <returns>The match, or <see langword="null"/> when no route matches.</returns>
    public static MockMatch? Match(IReadOnlyList<MockRoute> routes, string method, string path)
    {
        var upper = method.ToUpperInvariant();
        var segments = Split(path);
        foreach (var route in routes)
        {
            if (!route.Enabled || !MethodMatches(route.Method, upper))
            {
                continue;
            }

            var captured = new Dictionary<string, string>(StringComparer.Ordinal);
            if (PathMatches(Split(route.Path), segments, captured))
            {
                return new MockMatch(route, captured);
            }
        }

        return null;
    }

    private static bool MethodMatches(string routeMethod, string method) =>
        routeMethod == MockMethods.Any || routeMethod == method || (method == "HEAD" && routeMethod == "GET");

    private static string[] Split(string path)
    {
        if (path.Length > 1 && path[^1] == '/')
        {
            path = path[..^1];
        }

        var trimmed = path.StartsWith('/') ? path[1..] : path;
        return trimmed.Length == 0 ? [] : trimmed.Split('/');
    }

    private static bool PathMatches(string[] template, string[] actual, Dictionary<string, string> captured)
    {
        for (var i = 0; i < template.Length; i++)
        {
            var t = template[i];
            if (t == "*")
            {
                captured["*"] = string.Join('/', actual.Skip(i));
                return true;
            }

            if (i >= actual.Length)
            {
                return false;
            }

            if (IsParam(t))
            {
                if (actual[i].Length == 0)
                {
                    return false;
                }

                captured[t[0] == ':' ? t[1..] : t[1..^1]] = actual[i];
            }
            else if (!string.Equals(t, actual[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return template.Length == actual.Length;
    }

    private static bool IsParam(string s) =>
        (s.Length > 1 && s[0] == ':') || (s.Length > 2 && s[0] == '{' && s[^1] == '}');
}
