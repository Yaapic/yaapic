using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Insomnia;

/// <summary>Maps the <c>authentication</c> object of an Insomnia request.</summary>
internal static class InsomniaAuth
{
    /// <summary>Maps an Insomnia <c>authentication</c> object.</summary>
    /// <param name="authentication">The node, possibly null.</param>
    /// <param name="path">The report path of the request.</param>
    /// <param name="report">Receives the warnings and infos.</param>
    /// <returns>The mapped authentication; <see cref="RequestAuth.None"/> when absent, disabled or unsupported.</returns>
    internal static RequestAuth Map(DataNode? authentication, string path, ImportReport report)
    {
        if (authentication is not { IsObject: true } || authentication.GetBool("disabled") == true
            || authentication.GetString("type") is not { Length: > 0 } type)
        {
            return RequestAuth.None;
        }

        string S(string key) => InsomniaTemplates.Translate(authentication.GetString(key) ?? "", path, report);

        RequestAuth? mapped;
        switch (type)
        {
            case "none":
                return RequestAuth.None;
            case "basic":
                mapped = RequestAuth.None with { Kind = AuthKind.Basic, Username = S("username"), Password = S("password") };
                break;
            case "digest":
                mapped = RequestAuth.None with { Kind = AuthKind.Digest, Username = S("username"), Password = S("password") };
                break;
            case "bearer":
                var prefix = authentication.GetString("prefix") ?? "";
                if (prefix.Length > 0 && prefix != "Bearer")
                {
                    report.Warning(path, Strings.ImportAuthUnsupported(path, "bearer prefix " + prefix));
                }

                mapped = RequestAuth.None with { Kind = AuthKind.Bearer, Token = S("token") };
                break;
            case "apikey":
                mapped = RequestAuth.None with
                {
                    Kind = AuthKind.ApiKey,
                    Key = S("key"),
                    Value = S("value"),
                    Location = authentication.GetString("addTo") == "queryParams" ? ApiKeyLocation.Query : ApiKeyLocation.Header,
                };
                break;
            case "iam":
                mapped = RequestAuth.None with
                {
                    Kind = AuthKind.AwsV4,
                    AwsV4 = new AwsV4Settings(S("accessKeyId"), S("secretAccessKey"), S("sessionToken"), S("region"), S("service")),
                };
                break;
            case "oauth2":
                mapped = OAuth2(authentication, S, path, report);
                break;
            default:
                mapped = null;
                report.Warning(path, Strings.ImportAuthUnsupported(path, type));
                break;
        }

        if (mapped is null)
        {
            return RequestAuth.None;
        }

        if (HasLiteralCredential(mapped))
        {
            report.AddOnce(ImportSeverity.Info, "", Strings.ImportAuthPlainText);
        }

        return mapped;
    }

    private static RequestAuth? OAuth2(DataNode a, Func<string, string> s, string path, ImportReport report)
    {
        var grantName = a.GetString("grantType") is { Length: > 0 } g ? g : "authorization_code";
        var grant = grantName switch
        {
            "client_credentials" => OAuth2Grant.ClientCredentials,
            "authorization_code" => OAuth2Grant.AuthorizationCode,
            "password" => OAuth2Grant.Password,
            _ => (OAuth2Grant?)null,
        };
        if (grant is null)
        {
            report.Warning(path, Strings.ImportAuthUnsupported(path, "oauth2 " + grantName));
            return null;
        }

        var d = OAuth2Settings.Default;
        return RequestAuth.None with
        {
            Kind = AuthKind.OAuth2,
            OAuth2 = new OAuth2Settings(
                grant.Value,
                s("accessTokenUrl"),
                s("authorizationUrl"),
                s("clientId"),
                s("clientSecret"),
                s("scope"),
                s("audience"),
                s("username"),
                s("password"),
                s("redirectUrl"),
                a.GetBool("credentialsInBody") == true ? OAuth2ClientAuthentication.RequestBody : OAuth2ClientAuthentication.BasicHeader,
                OAuth2TokenPlacement.Header,
                a.GetString("headerPrefix") ?? d.HeaderPrefix,
                d.QueryParameterName),
        };
    }

    internal static bool HasLiteralCredential(RequestAuth a) =>
        new[]
        {
            a.Username, a.Password, a.Token, a.Value,
            a.AwsV4?.AccessKeyId, a.AwsV4?.SecretAccessKey, a.AwsV4?.SessionToken,
            a.OAuth2?.ClientSecret, a.OAuth2?.Password,
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
