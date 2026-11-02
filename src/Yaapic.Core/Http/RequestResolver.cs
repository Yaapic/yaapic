using System.Text;
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
        var body = request.Method.AllowsBody() && request.Body.Kind != BodyKind.None ? request.Body : null;

        string? bodyText = null;
        string? contentType = null;
        string? filePath = null;
        List<ResolvedPart>? parts = null;
        switch (body?.Kind)
        {
            case BodyKind.Raw:
                bodyText = Sub(body.Text);
                contentType = body.ContentType switch
                {
                    RawContentType.Json => "application/json",
                    RawContentType.Xml => "application/xml",
                    RawContentType.Html => "text/html; charset=utf-8",
                    RawContentType.JavaScript => "application/javascript",
                    _ => "text/plain; charset=utf-8",
                };
                break;
            case BodyKind.UrlEncoded:
                bodyText = string.Join('&', ResolveFields(body.Fields, Sub)
                    .Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
                contentType = "application/x-www-form-urlencoded";
                break;
            case BodyKind.FormData:
                parts = [.. ResolveFields(body.Fields, Sub).Select(f => f.IsFile
                    ? new ResolvedPart(f.Key, null, f.Value)
                    : new ResolvedPart(f.Key, f.Value, null))];
                break;
            case BodyKind.Binary:
                var path = Sub(body.FilePath);
                if (path.Length > 0)
                {
                    filePath = path;
                }

                contentType = "application/octet-stream";
                break;
        }

        ApplyAuth(request.Auth, Sub, headers, query);

        if (query.Count > 0)
        {
            url = AppendQuery(url, query);
        }

        if (body is not null)
        {
            var user = headers.Find(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
            if (user is not null)
            {
                if (parts is null)
                {
                    contentType = user.Value;
                }

                headers.RemoveAll(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
            }
        }

        return new(request.Method, url, headers, bodyText, contentType, unresolved, parts, filePath,
            request.Settings.FollowRedirects, request.Settings.TimeoutSeconds);
    }

    static void ApplyAuth(RequestAuth auth, Func<string?, string> sub, List<KeyValueEntry> headers, List<KeyValueEntry> query)
    {
        void AddHeader(string name, string value)
        {
            if (!headers.Exists(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                headers.Add(new(name, value));
            }
        }

        switch (auth.Kind)
        {
            case AuthKind.Basic:
                var user = sub(auth.Username);
                var pass = sub(auth.Password);
                if (user.Length > 0 || pass.Length > 0)
                {
                    AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")));
                }

                break;
            case AuthKind.Bearer:
                var token = sub(auth.Token).Trim();
                if (token.Length > 0)
                {
                    AddHeader("Authorization", "Bearer " + token);
                }

                break;
            case AuthKind.ApiKey:
                var key = sub(auth.Key);
                var value = sub(auth.Value);
                if (key.Length == 0)
                {
                    break;
                }

                if (auth.Location == ApiKeyLocation.Header)
                {
                    AddHeader(key, value);
                }
                else
                {
                    query.Add(new(key, value));
                }

                break;
        }
    }

    static List<(string Key, string Value, bool IsFile)> ResolveFields(IReadOnlyList<FormField> fields, Func<string?, string> sub) =>
        [.. fields.Where(f => f.Enabled)
            .Select(f => (Key: sub(f.Key), Value: sub(f.Value), IsFile: f.Kind == FormFieldKind.File))
            .Where(f => f.Key.Length > 0)];

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
