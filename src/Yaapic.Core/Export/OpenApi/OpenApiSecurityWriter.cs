using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Export.OpenApi;

/// <summary>Collects the <c>securitySchemes</c> of a document and the <c>security</c> requirement of each request.</summary>
internal sealed class OpenApiSecurityWriter(CollectionNode root)
{
    private readonly Dictionary<string, Scheme> _byKey = [];
    private readonly HashSet<string> _names = [];
    private readonly bool _hasBearer = AnyRequest(root, r => r.Auth.Kind == AuthKind.Bearer);

    /// <summary>Registers the authentication of a request.</summary>
    /// <param name="auth">The request authentication.</param>
    /// <returns>The <c>security</c> array of the operation, or null when the request has no authentication.</returns>
    internal DataNode? Requirement(RequestAuth auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        var scheme = auth.Kind switch
        {
            AuthKind.Basic => Register("basic", "basicAuth", _ => Http("basic")),
            AuthKind.Bearer => Register("bearer", "bearerAuth", _ => Http("bearer")),
            AuthKind.Jwt => Register("jwt", _hasBearer ? "jwtAuth" : "bearerAuth", _ => Http("bearer", "JWT")),
            AuthKind.Digest => Register("digest", "digestAuth", _ => Http("digest")),
            AuthKind.ApiKey => ApiKey(auth),
            AuthKind.OAuth2 when auth.OAuth2 is { } oauth => OAuth2(oauth),
            AuthKind.AwsV4 => Register("aws", "awsSigV4", _ => DataNodeJson.Obj(
                ("type", DataNodeJson.Str("apiKey")),
                ("name", DataNodeJson.Str("Authorization")),
                ("in", DataNodeJson.Str("header")),
                ("description", DataNodeJson.Str("AWS Signature Version 4")))),
            _ => null,
        };
        if (scheme is null) return null;

        var required = auth.Kind == AuthKind.OAuth2 ? Words(auth.OAuth2!.Scope) : [];
        return DataNodeJson.Arr(DataNodeJson.Obj((scheme.Name, DataNodeJson.Arr([.. required.Select(DataNodeJson.Str)]))));
    }

    /// <summary>Builds the <c>components</c> object.</summary>
    /// <returns>The object, or null when no scheme was registered.</returns>
    internal DataNode? Components() => _byKey.Count == 0
        ? null
        : DataNodeJson.Obj(("securitySchemes", DataNodeJson.Obj([.. _byKey.Values.Select(s => (s.Name, s.Definition(s.Scopes)))])));

    private static bool AnyRequest(CollectionNode node, Func<HttpRequestDefinition, bool> predicate) =>
        node.Kind == CollectionNodeKind.Folder ? node.Children.Any(c => AnyRequest(c, predicate)) : predicate(node.Request!);

    private static string[] Words(string scope) => scope.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static DataNode Http(string scheme, string? format = null) => format is null
        ? DataNodeJson.Obj(("type", DataNodeJson.Str("http")), ("scheme", DataNodeJson.Str(scheme)))
        : DataNodeJson.Obj(("type", DataNodeJson.Str("http")), ("scheme", DataNodeJson.Str(scheme)), ("bearerFormat", DataNodeJson.Str(format)));

    private Scheme ApiKey(RequestAuth auth)
    {
        var location = auth.Location == ApiKeyLocation.Query ? "query" : "header";
        return Register($"apikey|{location}|{auth.Key}", "apiKeyAuth", _ => DataNodeJson.Obj(
            ("type", DataNodeJson.Str("apiKey")),
            ("name", DataNodeJson.Str(auth.Key)),
            ("in", DataNodeJson.Str(location))));
    }

    private Scheme OAuth2(OAuth2Settings oauth)
    {
        var flow = oauth.Grant switch
        {
            OAuth2Grant.AuthorizationCode => "authorizationCode",
            OAuth2Grant.Password => "password",
            _ => "clientCredentials",
        };
        var authorization = oauth.Grant == OAuth2Grant.AuthorizationCode;
        var scheme = Register($"oauth2|{flow}|{oauth.TokenUrl}|{(authorization ? oauth.AuthorizationUrl : "")}", "oauth2Auth", scopes =>
        {
            List<(string, DataNode)> members = [("tokenUrl", DataNodeJson.Str(oauth.TokenUrl))];
            if (authorization) members.Insert(0, ("authorizationUrl", DataNodeJson.Str(oauth.AuthorizationUrl)));
            members.Add(("scopes", DataNodeJson.Obj([.. scopes.Select(s => (s, DataNodeJson.Str("")))])));
            return DataNodeJson.Obj(
                ("type", DataNodeJson.Str("oauth2")),
                ("flows", DataNodeJson.Obj((flow, DataNodeJson.Obj([.. members])))));
        });
        foreach (var word in Words(oauth.Scope).Where(w => !scheme.Scopes.Contains(w)))
        {
            scheme.Scopes.Add(word);
        }

        return scheme;
    }

    private Scheme Register(string key, string baseName, Func<IReadOnlyList<string>, DataNode> definition)
    {
        if (_byKey.TryGetValue(key, out var existing)) return existing;
        var name = baseName;
        for (var n = 2; !_names.Add(name); n++) name = baseName + n;
        return _byKey[key] = new Scheme(name, definition, []);
    }

    private sealed record Scheme(string Name, Func<IReadOnlyList<string>, DataNode> Definition, List<string> Scopes);
}
