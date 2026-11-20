using Yaapic.Core.Import;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Export.Postman;

/// <summary>Writes the <c>auth</c> block of a Postman v2.1 request: the inverse of the import mapping.</summary>
internal static class PostmanAuthWriter
{
    /// <summary>Writes an authentication.</summary>
    /// <param name="auth">The request authentication.</param>
    /// <returns>The <c>auth</c> node, or null for <see cref="AuthKind.None"/>.</returns>
    internal static DataNode? Write(RequestAuth auth)
    {
        switch (auth.Kind)
        {
            case AuthKind.Basic: return Block("basic", ("username", auth.Username), ("password", auth.Password));
            case AuthKind.Bearer: return Block("bearer", ("token", auth.Token));
            case AuthKind.ApiKey:
                return Block("apikey", ("key", auth.Key), ("value", auth.Value), ("in", auth.Location == ApiKeyLocation.Query ? "query" : "header"));
            case AuthKind.Digest: return Block("digest", ("username", auth.Username), ("password", auth.Password));
            case AuthKind.AwsV4 when auth.AwsV4 is { } a:
                return Block("awsv4", ("accessKey", a.AccessKeyId), ("secretKey", a.SecretAccessKey), ("sessionToken", a.SessionToken),
                    ("region", a.Region), ("service", a.Service));
            case AuthKind.OAuth2 when auth.OAuth2 is { } o:
                return Block("oauth2",
                    ("grant_type", o.Grant switch
                    {
                        OAuth2Grant.AuthorizationCode => "authorization_code",
                        OAuth2Grant.Password => "password_credentials",
                        _ => "client_credentials",
                    }),
                    ("accessTokenUrl", o.TokenUrl), ("authUrl", o.AuthorizationUrl), ("clientId", o.ClientId), ("clientSecret", o.ClientSecret),
                    ("scope", o.Scope), ("audience", o.Audience), ("username", o.Username), ("password", o.Password),
                    ("redirect_uri", o.RedirectUri),
                    ("addTokenTo", o.Placement == OAuth2TokenPlacement.Query ? "queryParams" : "header"),
                    ("headerPrefix", o.HeaderPrefix),
                    ("client_authentication", o.ClientAuthentication == OAuth2ClientAuthentication.RequestBody ? "body" : "header"));
            case AuthKind.Jwt when auth.Jwt is { } j:
                return Block("jwt",
                    ("algorithm", j.Algorithm.ToString().ToUpperInvariant()), ("secret", j.Secret),
                    ("isSecretBase64Encoded", j.SecretIsBase64 ? "true" : "false"), ("privateKey", j.PrivateKey),
                    ("payload", j.Payload), ("header", j.ExtraHeaders),
                    ("addTokenTo", j.Placement == JwtPlacement.Query ? "queryParams" : "header"),
                    ("headerPrefix", j.HeaderPrefix), ("queryParamKey", j.QueryParameterName));
            default: return null;
        }
    }

    private static DataNode Block(string type, params (string Key, string Value)[] parameters) =>
        DataNodeJson.Obj(
            ("type", DataNodeJson.Str(type)),
            (type, DataNodeJson.Arr([.. parameters.Select(p => DataNodeJson.Obj(
                ("key", DataNodeJson.Str(p.Key)), ("value", DataNodeJson.Str(p.Value)), ("type", DataNodeJson.Str("string"))))])));
}
