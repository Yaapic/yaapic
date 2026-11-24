using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Postman;

/// <summary>Maps the <c>auth</c> block of a Postman collection, folder or request.</summary>
internal static class PostmanAuth
{
    /// <summary>Maps a Postman <c>auth</c> block.</summary>
    /// <param name="auth">The <c>auth</c> node, possibly null.</param>
    /// <param name="path">The report path of the owner of the block.</param>
    /// <param name="report">Receives the warnings and infos.</param>
    /// <returns>Null when there is no block (inherit), <see cref="RequestAuth.None"/> for <c>noauth</c> and unsupported types, else the mapped authentication.</returns>
    internal static RequestAuth? Map(DataNode? auth, string path, ImportReport report)
    {
        if (auth is not { IsObject: true } || auth.GetString("type") is not { Length: > 0 } type)
        {
            return null;
        }

        var p = Parameters(auth[type], path, report);
        var mapped = type switch
        {
            "noauth" => RequestAuth.None,
            "basic" => Credentials(AuthKind.Basic, p),
            "digest" => Credentials(AuthKind.Digest, p),
            "bearer" => RequestAuth.None with { Kind = AuthKind.Bearer, Token = Get(p, "token") },
            "apikey" => RequestAuth.None with
            {
                Kind = AuthKind.ApiKey,
                Key = Get(p, "key"),
                Value = Get(p, "value"),
                Location = Get(p, "in") == "query" ? ApiKeyLocation.Query : ApiKeyLocation.Header,
            },
            "awsv4" => RequestAuth.None with
            {
                Kind = AuthKind.AwsV4,
                AwsV4 = new AwsV4Settings(Get(p, "accessKey"), Get(p, "secretKey"), Get(p, "sessionToken"), Get(p, "region"), Get(p, "service")),
            },
            "oauth2" => OAuth2(p, path, report),
            "jwt" => Jwt(p, path, report),
            _ => null,
        };

        if (mapped is null)
        {
            report.Warning(path, Strings.ImportAuthUnsupported(path, type));
            return RequestAuth.None;
        }

        if (HasLiteralCredential(mapped))
        {
            report.AddOnce(ImportSeverity.Info, "", Strings.ImportAuthPlainText);
        }

        return mapped;
    }

    private static RequestAuth Credentials(AuthKind kind, Dictionary<string, string> p) =>
        RequestAuth.None with { Kind = kind, Username = Get(p, "username"), Password = Get(p, "password") };

    private static RequestAuth? OAuth2(Dictionary<string, string> p, string path, ImportReport report)
    {
        var grantName = p.GetValueOrDefault("grant_type", "authorization_code");
        OAuth2Grant grant;
        switch (grantName)
        {
            case "client_credentials":
                grant = OAuth2Grant.ClientCredentials;
                break;
            case "authorization_code" or "authorization_code_with_pkce":
                grant = OAuth2Grant.AuthorizationCode;
                break;
            case "password_credentials":
                grant = OAuth2Grant.Password;
                break;
            default:
                report.Warning(path, Strings.ImportAuthUnsupported(path, "oauth2 " + grantName));
                return RequestAuth.None;
        }

        var d = OAuth2Settings.Default;
        return RequestAuth.None with
        {
            Kind = AuthKind.OAuth2,
            OAuth2 = new OAuth2Settings(
                grant,
                Get(p, "accessTokenUrl"),
                Get(p, "authUrl"),
                Get(p, "clientId"),
                Get(p, "clientSecret"),
                Get(p, "scope"),
                Get(p, "audience"),
                Get(p, "username"),
                Get(p, "password"),
                Get(p, "redirect_uri"),
                Get(p, "client_authentication") == "body" ? OAuth2ClientAuthentication.RequestBody : OAuth2ClientAuthentication.BasicHeader,
                Get(p, "addTokenTo") == "queryParams" ? OAuth2TokenPlacement.Query : OAuth2TokenPlacement.Header,
                p.GetValueOrDefault("headerPrefix", d.HeaderPrefix),
                d.QueryParameterName),
        };
    }

    private static RequestAuth Jwt(Dictionary<string, string> p, string path, ImportReport report)
    {
        var algorithm = JwtAlgorithm.Hs256;
        if (p.TryGetValue("algorithm", out var name) && name.Length > 0)
        {
            switch (name.ToUpperInvariant())
            {
                case "HS256": break;
                case "HS384": algorithm = JwtAlgorithm.Hs384; break;
                case "HS512": algorithm = JwtAlgorithm.Hs512; break;
                case "RS256": algorithm = JwtAlgorithm.Rs256; break;
                case "RS384": algorithm = JwtAlgorithm.Rs384; break;
                case "RS512": algorithm = JwtAlgorithm.Rs512; break;
                case "ES256": algorithm = JwtAlgorithm.Es256; break;
                case "ES384": algorithm = JwtAlgorithm.Es384; break;
                case "ES512": algorithm = JwtAlgorithm.Es512; break;
                default:
                    report.Warning(path, Strings.ImportAuthUnsupported(path, "jwt " + name));
                    break;
            }
        }

        var d = JwtSettings.Default;
        return RequestAuth.None with
        {
            Kind = AuthKind.Jwt,
            Jwt = new JwtSettings(
                algorithm,
                Get(p, "secret"),
                Get(p, "isSecretBase64Encoded") == "true",
                Get(p, "privateKey"),
                p.TryGetValue("payload", out var payload) && payload.Length > 0 ? payload : d.Payload,
                Get(p, "header"),
                true,
                0,
                Get(p, "addTokenTo") == "queryParams" ? JwtPlacement.Query : JwtPlacement.Header,
                p.GetValueOrDefault("headerPrefix", d.HeaderPrefix),
                p.GetValueOrDefault("queryParamKey", d.QueryParameterName)),
        };
    }

    /// <summary>Reads the parameters of an auth type, given as an array of <c>{key, value}</c> or as an object (v2.0).</summary>
    private static Dictionary<string, string> Parameters(DataNode? node, string path, ImportReport report)
    {
        Dictionary<string, string> result = [];
        if (node is { IsArray: true })
        {
            foreach (var e in node.Items)
            {
                if (e.GetString("key") is { Length: > 0 } key)
                {
                    result[key] = Translate(e.GetString("value") ?? "", path, report);
                }
            }
        }
        else if (node is { IsObject: true })
        {
            foreach (var (key, value) in node.Members)
            {
                result[key] = Translate(value.AsString() ?? "", path, report);
            }
        }

        return result;
    }

    private static string Translate(string value, string path, ImportReport report) =>
        ImportHelpers.TranslateDynamicVariables(value, path, report);

    private static string Get(Dictionary<string, string> p, string key) => p.GetValueOrDefault(key, "");

    private static bool HasLiteralCredential(RequestAuth a) =>
        new[]
        {
            a.Username, a.Password, a.Token, a.Value,
            a.AwsV4?.AccessKeyId, a.AwsV4?.SecretAccessKey, a.AwsV4?.SessionToken,
            a.OAuth2?.ClientSecret, a.OAuth2?.Password,
            a.Jwt?.Secret, a.Jwt?.PrivateKey,
        }.Any(IsLiteral);

    private static bool IsLiteral(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var v = value.Trim();
        var isVariable = v.StartsWith("{{", StringComparison.Ordinal)
            && v.EndsWith("}}", StringComparison.Ordinal)
            && v.IndexOf("}}", StringComparison.Ordinal) == v.Length - 2;
        return !isVariable;
    }
}
