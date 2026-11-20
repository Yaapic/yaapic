using System.Globalization;
using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Import;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Bruno;

/// <summary>Writes the text of a Bruno <c>.bru</c> request file; the inverse of the import in <c>Import/Bruno</c>.</summary>
internal static class BruWriter
{
    private const string Indent = "  ";
    private const string MultiLineMarker = "'''";
    private const string ScriptComment = "// Exported from Yaapic: this script uses the yaapic API, rewrite it for Bruno.";

    /// <summary>Writes one request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="seq">The 1-based position among its siblings.</param>
    /// <param name="path">The report path of the request.</param>
    /// <param name="report">Receives the warnings.</param>
    /// <returns>The file text, ending with a single newline.</returns>
    internal static string WriteRequest(HttpRequestDefinition request, int seq, string path, ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(report);

        var parts = QueryStringSync.Parse(request.Url);
        var url = QueryStringSync.Compose(parts.Prefix, [.. request.QueryParams.Where(q => q.Enabled)], parts.Fragment);
        var (bodyMode, bodyBlock) = Body(request.Body, path, report);
        var (authMode, authBlock) = Auth(request.Auth, path, report);

        List<string> blocks =
        [
            Dictionary("meta", [("name", request.Name), ("type", "http"), ("seq", seq.ToString(CultureInfo.InvariantCulture))]),
            Dictionary(request.Method.ToText().ToLowerInvariant(), [("url", url), ("body", bodyMode), ("auth", authMode)]),
        ];
        AddIfAny(blocks, "params:query", request.QueryParams.Select(q => (q.Enabled ? q.Key : "~" + q.Key, q.Value)));
        AddIfAny(blocks, "headers", request.Headers.Select(h => (h.Enabled ? h.Key : "~" + h.Key, h.Value)));
        if (authBlock is not null)
        {
            blocks.Add(authBlock);
        }

        if (bodyBlock is not null)
        {
            blocks.Add(bodyBlock);
        }

        foreach (var (name, block) in new[] { ("script:pre-request", request.Scripts.PreRequest), ("script:post-response", request.Scripts.PostResponse) })
        {
            if (block.IsEmpty)
            {
                continue;
            }

            if (block.Language != ScriptLanguage.JavaScript)
            {
                report.Warning(path, Strings.ExportBrunoLuaScript(path));
                continue;
            }

            blocks.Add(Text(name, ScriptComment + "\n" + block.Source));
        }

        return string.Join("\n\n", blocks) + "\n";
    }

    private static (string Mode, string? Block) Body(RequestBody body, string path, ImportReport report)
    {
        switch (body.Kind)
        {
            case BodyKind.Raw:
                var (mode, name) = body.ContentType switch
                {
                    RawContentType.Json => ("json", "body:json"),
                    RawContentType.Xml => ("xml", "body:xml"),
                    _ => ("text", "body:text"),
                };
                return (mode, string.IsNullOrEmpty(body.Text) ? null : Text(name, body.Text));
            case BodyKind.UrlEncoded:
                return ("formUrlEncoded", Pairs("body:form-urlencoded", body.Fields.Select(f => (f.Enabled ? f.Key : "~" + f.Key, f.Value))));
            case BodyKind.FormData:
                return ("multipartForm", Pairs("body:multipart-form",
                    body.Fields.Select(f => (f.Enabled ? f.Key : "~" + f.Key, f.Kind == FormFieldKind.File ? "@file(" + f.Value + ")" : f.Value))));
            case BodyKind.Binary:
                report.Warning(path, Strings.ExportBrunoBinaryBody(path));
                return ("none", null);
            default:
                return ("none", null);
        }
    }

    private static (string Mode, string? Block) Auth(RequestAuth a, string path, ImportReport report)
    {
        switch (a.Kind)
        {
            case AuthKind.Basic:
                return ("basic", Dictionary("auth:basic", [("username", a.Username), ("password", a.Password)]));
            case AuthKind.Digest:
                return ("digest", Dictionary("auth:digest", [("username", a.Username), ("password", a.Password)]));
            case AuthKind.Bearer:
                return ("bearer", Dictionary("auth:bearer", [("token", a.Token)]));
            case AuthKind.ApiKey:
                return ("apikey", Dictionary("auth:apikey",
                    [("key", a.Key), ("value", a.Value), ("placement", a.Location == ApiKeyLocation.Query ? "queryparams" : "header")]));
            case AuthKind.AwsV4 when a.AwsV4 is { } aws:
                return ("awsv4", Dictionary("auth:awsv4",
                [
                    ("accessKeyId", aws.AccessKeyId), ("secretAccessKey", aws.SecretAccessKey), ("sessionToken", aws.SessionToken),
                    ("service", aws.Service), ("region", aws.Region), ("profileName", ""),
                ]));
            case AuthKind.OAuth2 when a.OAuth2 is { } o:
                var grant = o.Grant switch
                {
                    OAuth2Grant.AuthorizationCode => "authorization_code",
                    OAuth2Grant.Password => "password",
                    _ => "client_credentials",
                };
                return ("oauth2", Dictionary("auth:oauth2",
                [
                    ("grant_type", grant), ("callback_url", o.RedirectUri), ("authorization_url", o.AuthorizationUrl),
                    ("access_token_url", o.TokenUrl), ("client_id", o.ClientId), ("client_secret", o.ClientSecret),
                    ("scope", o.Scope), ("username", o.Username), ("password", o.Password),
                    ("token_placement", o.Placement == OAuth2TokenPlacement.Query ? "query" : "header"),
                    ("token_header_prefix", o.HeaderPrefix), ("token_query_key", o.QueryParameterName),
                    ("credentials_placement", o.ClientAuthentication == OAuth2ClientAuthentication.RequestBody ? "body" : "basic_auth_header"),
                ]));
            case AuthKind.Jwt:
                report.Warning(path, Strings.ExportBrunoAuthUnsupported(path, "jwt"));
                return ("none", null);
            default:
                return ("none", null);
        }
    }

    private static void AddIfAny(List<string> blocks, string name, IEnumerable<(string Key, string Value)> rows)
    {
        if (Pairs(name, rows) is { } block)
        {
            blocks.Add(block);
        }
    }

    private static string? Pairs(string name, IEnumerable<(string Key, string Value)> rows)
    {
        var list = rows.ToList();
        return list.Count == 0 ? null : Dictionary(name, list);
    }

    private static string Dictionary(string name, IReadOnlyList<(string Key, string Value)> rows)
    {
        StringBuilder sb = new();
        sb.Append(name).Append(" {\n");
        foreach (var (key, raw) in rows)
        {
            var value = Normalize(raw);
            if (value.Contains('\n', StringComparison.Ordinal))
            {
                sb.Append(Indent).Append(key).Append(": ").Append(MultiLineMarker).Append('\n');
                foreach (var line in value.Split('\n'))
                {
                    sb.Append(Indent).Append(line).Append('\n');
                }

                sb.Append(Indent).Append(MultiLineMarker).Append('\n');
            }
            else
            {
                sb.Append(Indent).Append(key).Append(": ").Append(value).Append('\n');
            }
        }

        return sb.Append('}').ToString();
    }

    private static string Text(string name, string text)
    {
        StringBuilder sb = new();
        sb.Append(name).Append(" {\n");
        foreach (var line in Normalize(text).TrimEnd('\n').Split('\n'))
        {
            sb.Append(line.Length == 0 ? "" : Indent + line).Append('\n');
        }

        return sb.Append('}').ToString();
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
