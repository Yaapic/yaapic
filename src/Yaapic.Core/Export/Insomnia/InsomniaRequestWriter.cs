using Yaapic.Core.Http;
using Yaapic.Core.Import;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Insomnia;

/// <summary>Writes the <c>request</c> resources of an Insomnia v4 export.</summary>
/// <param name="report">Receives the warnings.</param>
internal sealed class InsomniaRequestWriter(ImportReport report)
{
    /// <summary>Writes a request resource.</summary>
    /// <param name="id">The resource identifier.</param>
    /// <param name="parentId">The parent resource identifier.</param>
    /// <param name="request">The request.</param>
    /// <param name="position">The 0-based position among its siblings.</param>
    /// <param name="path">The report path.</param>
    /// <returns>The resource node.</returns>
    internal DataNode Write(string id, string parentId, HttpRequestDefinition request, int position, string path)
    {
        var parts = QueryStringSync.Parse(request.Url);
        var url = QueryStringSync.Compose(parts.Prefix, [.. request.QueryParams.Where(q => q.Enabled)], parts.Fragment);
        return DataNodeJson.Obj(
            ("_id", DataNodeJson.Str(id)),
            ("_type", DataNodeJson.Str("request")),
            ("parentId", DataNodeJson.Str(parentId)),
            ("name", DataNodeJson.Str(request.Name)),
            ("description", DataNodeJson.Str("")),
            ("method", DataNodeJson.Str(request.Method.ToText())),
            ("url", DataNodeJson.Str(T(url, path))),
            ("parameters", Pairs([.. request.QueryParams.Where(q => !q.Enabled).Select(q => (q.Key, q.Value, q.Enabled))], path)),
            ("headers", Pairs([.. request.Headers.Select(h => (h.Key, h.Value, h.Enabled))], path)),
            ("body", Body(request.Body, path)),
            ("authentication", Auth(request.Auth, path)),
            ("settingFollowRedirects", DataNodeJson.Str(request.Settings.FollowRedirects ? "on" : "off")),
            ("metaSortKey", DataNodeJson.Num(position)));
    }

    private string T(string text, string path) => InsomniaTemplateWriter.Translate(text, path, report);

    private DataNode Pairs(IEnumerable<(string Name, string Value, bool Enabled)> rows, string path) =>
        DataNodeJson.Arr([.. rows.Select(r => Pair(r.Name, r.Value, r.Enabled, path))]);

    private DataNode Pair(string name, string value, bool enabled, string path)
    {
        List<(string, DataNode)> members = [("name", DataNodeJson.Str(T(name, path))), ("value", DataNodeJson.Str(T(value, path)))];
        if (!enabled) members.Add(("disabled", DataNode.Bool(true)));
        return DataNodeJson.Obj([.. members]);
    }

    private DataNode Body(RequestBody body, string path)
    {
        switch (body.Kind)
        {
            case BodyKind.Raw:
                var mime = body.ContentType switch
                {
                    RawContentType.Json => "application/json",
                    RawContentType.Xml => "application/xml",
                    RawContentType.Html => "text/html",
                    RawContentType.JavaScript => "application/javascript",
                    _ => "text/plain",
                };
                return DataNodeJson.Obj(("mimeType", DataNodeJson.Str(mime)), ("text", DataNodeJson.Str(T(body.Text, path))));
            case BodyKind.UrlEncoded:
                return DataNodeJson.Obj(("mimeType", DataNodeJson.Str("application/x-www-form-urlencoded")),
                    ("params", Pairs(body.Fields.Select(f => (f.Key, f.Value, f.Enabled)), path)));
            case BodyKind.FormData:
                return DataNodeJson.Obj(("mimeType", DataNodeJson.Str("multipart/form-data")),
                    ("params", DataNodeJson.Arr([.. body.Fields.Select(f => FormField(f, path))])));
            case BodyKind.Binary:
                return DataNodeJson.Obj(("mimeType", DataNodeJson.Str("application/octet-stream")), ("fileName", DataNodeJson.Str(body.FilePath)));
            default:
                return DataNodeJson.Obj();
        }
    }

    private DataNode FormField(FormField f, string path)
    {
        if (f.Kind != FormFieldKind.File) return Pair(f.Key, f.Value, f.Enabled, path);
        List<(string, DataNode)> members =
        [
            ("name", DataNodeJson.Str(T(f.Key, path))), ("value", DataNodeJson.Str("")),
            ("type", DataNodeJson.Str("file")), ("fileName", DataNodeJson.Str(f.Value)),
        ];
        if (!f.Enabled) members.Add(("disabled", DataNode.Bool(true)));
        return DataNodeJson.Obj([.. members]);
    }

    private DataNode Auth(RequestAuth a, string path)
    {
        DataNode Block(string type, params (string Key, string Value)[] fields) => DataNodeJson.Obj(
            [("type", DataNodeJson.Str(type)), .. fields.Select(f => (f.Key, DataNodeJson.Str(T(f.Value, path))))]);

        switch (a.Kind)
        {
            case AuthKind.Basic: return Block("basic", ("username", a.Username), ("password", a.Password));
            case AuthKind.Bearer: return Block("bearer", ("token", a.Token));
            case AuthKind.Digest: return Block("digest", ("username", a.Username), ("password", a.Password));
            case AuthKind.ApiKey:
                return Block("apikey", ("key", a.Key), ("value", a.Value), ("addTo", a.Location == ApiKeyLocation.Query ? "queryParams" : "header"));
            case AuthKind.AwsV4 when a.AwsV4 is { } aws:
                return Block("iam", ("accessKeyId", aws.AccessKeyId), ("secretAccessKey", aws.SecretAccessKey),
                    ("sessionToken", aws.SessionToken), ("region", aws.Region), ("service", aws.Service));
            case AuthKind.OAuth2 when a.OAuth2 is { } o:
                var grant = o.Grant switch
                {
                    OAuth2Grant.AuthorizationCode => "authorization_code",
                    OAuth2Grant.Password => "password",
                    _ => "client_credentials",
                };
                var fields = new (string Key, string Value)[]
                {
                    ("grantType", grant), ("accessTokenUrl", o.TokenUrl), ("authorizationUrl", o.AuthorizationUrl), ("clientId", o.ClientId),
                    ("clientSecret", o.ClientSecret), ("scope", o.Scope), ("audience", o.Audience), ("username", o.Username),
                    ("password", o.Password), ("redirectUrl", o.RedirectUri),
                };
                return DataNodeJson.Obj(
                [
                    ("type", DataNodeJson.Str("oauth2")),
                    .. fields.Select(f => (f.Key, DataNodeJson.Str(T(f.Value, path)))),
                    ("credentialsInBody", DataNode.Bool(o.ClientAuthentication == OAuth2ClientAuthentication.RequestBody)),
                    ("headerPrefix", DataNodeJson.Str(o.HeaderPrefix)),
                ]);
            case AuthKind.Jwt:
                report.Warning(path, Strings.ExportInsomniaAuthUnsupported(path, "jwt"));
                return DataNodeJson.Obj();
            default:
                return DataNodeJson.Obj();
        }
    }
}
