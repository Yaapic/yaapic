using Yaapic.Core.Variables;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>Turns a <see cref="HttpRequestDefinition"/> into a <see cref="ResolvedRequest"/>.</summary>
public static class RequestResolver
{
    /// <summary>Resolves variables, appends the query string and decides the body and its content type.</summary>
    /// <param name="request">The request definition.</param>
    /// <param name="source">The variable source.</param>
    /// <returns>The ready-to-send request.</returns>
    public static ResolvedRequest Resolve(HttpRequestDefinition request, IVariableSource source)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        List<string> unresolved = [];
        string Sub(string? text)
        {
            var r = VariableResolver.Resolve(text, source);
            foreach (var name in r.Unresolved)
            {
                if (!unresolved.Contains(name))
                {
                    unresolved.Add(name);
                }
            }

            return r.Text;
        }

        List<KeyValueEntry> Entries(IReadOnlyList<KeyValueEntry> list) =>
            [.. list.Where(e => e.Enabled)
                .Select(e => new KeyValueEntry(Sub(e.Key), Sub(e.Value)))
                .Where(e => e.Key.Length > 0)];

        var url = Sub(request.Url).Trim();
        if (url.Length > 0 && !url.Contains("://", StringComparison.Ordinal))
        {
            url = "http://" + url;
        }

        var query = Entries(request.QueryParams);
        var headers = Entries(request.Headers);
        var body = request.Method == HttpMethodKind.Post && request.Body.Kind == BodyKind.Raw ? request.Body : null;
        var bodyText = body is null ? null : Sub(body.Text);

        if (query.Count > 0)
        {
            url = AppendQuery(url, query);
        }

        string? contentType = null;
        if (body is not null)
        {
            contentType = body.ContentType switch
            {
                RawContentType.Json => "application/json",
                RawContentType.Xml => "application/xml",
                _ => "text/plain; charset=utf-8",
            };
            var user = headers.Find(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
            if (user is not null)
            {
                contentType = user.Value;
                headers.RemoveAll(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
            }
        }

        return new(request.Method, url, headers, bodyText, contentType, unresolved);
    }

    static string AppendQuery(string url, List<KeyValueEntry> query)
    {
        var hash = url.IndexOf('#', StringComparison.Ordinal);
        var fragment = hash >= 0 ? url[hash..] : "";
        var head = hash >= 0 ? url[..hash] : url;
        var pairs = string.Join('&', query.Select(e => $"{Uri.EscapeDataString(e.Key)}={Uri.EscapeDataString(e.Value)}"));
        var sep = !head.Contains('?', StringComparison.Ordinal) ? "?" : head.EndsWith('?') || head.EndsWith('&') ? "" : "&";
        return head + sep + pairs + fragment;
    }
}
