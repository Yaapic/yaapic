using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Mocks;

/// <summary>A request received by a mock server.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="RawTarget">The request target as received, such as <c>/users/1?x=2</c>.</param>
/// <param name="Path">The percent-decoded path without the query.</param>
/// <param name="Query">The query string without the leading <c>?</c>.</param>
/// <param name="Headers">The request headers.</param>
/// <param name="Body">The request body.</param>
/// <param name="HttpVersion">The HTTP version, such as <c>HTTP/1.1</c>.</param>
public sealed record MockHttpRequest(
    string Method, string RawTarget, string Path, string Query,
    IReadOnlyList<KeyValueEntry> Headers, byte[] Body, string HttpVersion)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The decoded query parameters, in order.</summary>
    public IReadOnlyList<KeyValueEntry> QueryValues => SplitQuery(Query);

    /// <summary>Splits a request target and builds the request; never throws.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="rawTarget">The request target, origin-form or absolute-form.</param>
    /// <param name="headers">The request headers.</param>
    /// <param name="body">The request body.</param>
    /// <param name="version">The HTTP version.</param>
    /// <returns>The parsed request.</returns>
    public static MockHttpRequest Parse(
        string method, string rawTarget, IReadOnlyList<KeyValueEntry> headers, byte[] body, string version)
    {
        var target = rawTarget;
        var scheme = target.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            var slash = target.IndexOfAny(['/', '?'], scheme + 3);
            target = slash < 0 ? "/" : target[slash..];
            if (target[0] == '?')
            {
                target = "/" + target;
            }
        }

        var hash = target.IndexOf('#');
        if (hash >= 0)
        {
            target = target[..hash];
        }

        var q = target.IndexOf('?');
        var path = q < 0 ? target : target[..q];
        var query = q < 0 ? "" : target[(q + 1)..];
        return new MockHttpRequest(method, rawTarget, Decode(path, false), query, headers, body, version);
    }

    /// <summary>Gets the first header with the given name, case-insensitively.</summary>
    /// <param name="name">The header name.</param>
    /// <returns>The value, or <see langword="null"/> when absent.</returns>
    public string? Header(string name) =>
        Headers.FirstOrDefault(h => string.Equals(h.Key, name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static List<KeyValueEntry> SplitQuery(string query)
    {
        var list = new List<KeyValueEntry>();
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            list.Add(eq < 0
                ? new KeyValueEntry(Decode(part, true), "")
                : new KeyValueEntry(Decode(part[..eq], true), Decode(part[(eq + 1)..], true)));
        }

        return list;
    }

    private static string Decode(string s, bool plusIsSpace)
    {
        var sb = new StringBuilder();
        var run = new List<byte>();
        var runStart = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 1]) && Uri.IsHexDigit(s[i + 2]))
            {
                if (run.Count == 0)
                {
                    runStart = i;
                }

                run.Add(Convert.ToByte(s.Substring(i + 1, 2), 16));
                i += 2;
                continue;
            }

            Flush(sb, run, s, runStart, i);
            sb.Append(plusIsSpace && s[i] == '+' ? ' ' : s[i]);
        }

        Flush(sb, run, s, runStart, s.Length);
        return sb.ToString();
    }

    private static void Flush(StringBuilder sb, List<byte> run, string s, int start, int end)
    {
        if (run.Count == 0)
        {
            return;
        }

        try
        {
            sb.Append(StrictUtf8.GetString(run.ToArray()));
        }
        catch (ArgumentException)
        {
            sb.Append(s, start, end - start);
        }

        run.Clear();
    }
}
