using Yaapic.Core.Runner;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;
using Yaapic.Localization;

namespace Yaapic.Core.Mocks;

/// <summary>The result of <see cref="MockGenerator.FromFolder"/>.</summary>
/// <param name="Server">The generated server definition.</param>
/// <param name="Skipped">The number of requests skipped because an earlier route has the same method and path.</param>
public sealed record MockGeneration(MockServerDefinition Server, int Skipped);

/// <summary>Builds a mock server definition from the requests of a folder.</summary>
public static class MockGenerator
{
    /// <summary>Creates one route per request of a folder, in run order.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The server and the number of duplicate routes skipped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="folder"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="folder"/> is a request node.</exception>
    public static MockGeneration FromFolder(CollectionNode folder)
    {
        List<MockRoute> routes = [];
        HashSet<(string, string)> seen = [];
        var skipped = 0;
        foreach (var item in RunPlanBuilder.ListRequests(folder))
        {
            var request = item.Request;
            var method = request.Method.ToText();
            var path = PathOf(request.Url);
            if (!seen.Add((method, path)))
            {
                skipped++;
                continue;
            }

            routes.Add(MockRoute.Create(request.Name) with { Method = method, Path = path });
        }

        var server = MockServerDefinition.Create(Strings.MockGeneratedName(folder.Name)) with { Routes = routes };
        return new MockGeneration(server, skipped);
    }

    /// <summary>Extracts the mock route path of a request URL template.</summary>
    /// <param name="url">The URL template.</param>
    /// <returns>The path, starting with <c>/</c>; single-token segments become <c>:name</c>.</returns>
    public static string PathOf(string url)
    {
        var spans = VariableScanner.ScanSpans(url);
        var text = CutQueryAndFragment(url, spans);
        text = RemoveHost(text, VariableScanner.ScanSpans(text));
        if (text.Length == 0)
        {
            return "/";
        }

        var segments = text.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var spans2 = VariableScanner.ScanSpans(segments[i]);
            if (spans2.Count == 1 && spans2[0].Length == segments[i].Length)
            {
                segments[i] = ":" + spans2[0].Name;
            }
        }

        return string.Join('/', segments);
    }

    private static string CutQueryAndFragment(string url, IReadOnlyList<VariableSpan> spans)
    {
        var inSpan = 0;
        for (var i = 0; i < url.Length; i++)
        {
            while (inSpan < spans.Count && spans[inSpan].End <= i)
            {
                inSpan++;
            }

            var inside = inSpan < spans.Count && i >= spans[inSpan].Start;
            if (!inside && url[i] is '?' or '#')
            {
                return url[..i];
            }
        }

        return url;
    }

    private static string RemoveHost(string text, IReadOnlyList<VariableSpan> spans)
    {
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0 && IsScheme(text.AsSpan(0, scheme)))
        {
            var slash = text.IndexOf('/', scheme + 3);
            return slash < 0 ? "" : text[slash..];
        }

        if (spans.Count > 0 && spans[0].Start == 0 && (spans[0].End == text.Length || text[spans[0].End] == '/'))
        {
            return text[spans[0].End..];
        }

        if (text.StartsWith('/'))
        {
            return text;
        }

        var first = text.IndexOf('/');
        return first < 0 ? "" : text[first..];
    }

    private static bool IsScheme(ReadOnlySpan<char> s)
    {
        if (!char.IsAsciiLetter(s[0]))
        {
            return false;
        }

        foreach (var c in s)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('+' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }
}
