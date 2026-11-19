using Yaapic.Core.Import.Insomnia;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Bruno;

/// <summary>Maps the <c>auth:&lt;mode&gt;</c> blocks of a Bru file to a <see cref="RequestAuth"/>.</summary>
internal static class BrunoAuth
{
    /// <summary>Maps the authentication of a request, folder or collection file.</summary>
    /// <param name="file">The parsed file.</param>
    /// <param name="mode">The auth mode named by the file (<c>none</c>, <c>inherit</c>, <c>bearer</c>...).</param>
    /// <param name="path">The report path.</param>
    /// <param name="report">Receives the warnings and infos.</param>
    /// <param name="authMode">How the authentication was resolved.</param>
    /// <returns>The mapped authentication, <see cref="RequestAuth.None"/> when there is none.</returns>
    internal static RequestAuth Map(BruFile file, string? mode, string path, ImportReport report, out BruAuthMode authMode)
    {
        authMode = BruAuthMode.None;
        if (mode is null or "" or "none")
        {
            return RequestAuth.None;
        }

        if (mode == "inherit")
        {
            authMode = BruAuthMode.Inherit;
            return RequestAuth.None;
        }

        var block = file.Get("auth:" + mode);
        string S(string key) => block?.Pairs.FirstOrDefault(p => p.Key == key)?.Value ?? "";

        RequestAuth? mapped = mode switch
        {
            "basic" => RequestAuth.None with { Kind = AuthKind.Basic, Username = S("username"), Password = S("password") },
            "digest" => RequestAuth.None with { Kind = AuthKind.Digest, Username = S("username"), Password = S("password") },
            "bearer" => RequestAuth.None with { Kind = AuthKind.Bearer, Token = S("token") },
            "awsv4" => RequestAuth.None with
            {
                Kind = AuthKind.AwsV4,
                AwsV4 = new AwsV4Settings(S("accessKeyId"), S("secretAccessKey"), S("sessionToken"), S("region"), S("service")),
            },
            "apikey" => RequestAuth.None with
            {
                Kind = AuthKind.ApiKey,
                Key = S("key"),
                Value = S("value"),
                Location = S("placement") == "queryparams" ? ApiKeyLocation.Query : ApiKeyLocation.Header,
            },
            "oauth2" => OAuth2(S, path, report),
            _ => null,
        };

        if (mapped is null)
        {
            if (mode != "oauth2")
            {
                report.Warning(path, Strings.ImportAuthUnsupported(path, mode));
            }

            return RequestAuth.None;
        }

        authMode = BruAuthMode.Mapped;
        if (InsomniaAuth.HasLiteralCredential(mapped))
        {
            report.AddOnce(ImportSeverity.Info, "", Strings.ImportAuthPlainText);
        }

        return mapped;
    }

    private static RequestAuth? OAuth2(Func<string, string> s, string path, ImportReport report)
    {
        var grantName = s("grant_type");
        OAuth2Grant? grant = grantName switch
        {
            "client_credentials" => OAuth2Grant.ClientCredentials,
            "authorization_code" => OAuth2Grant.AuthorizationCode,
            "password" => OAuth2Grant.Password,
            _ => null,
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
                s("access_token_url"),
                s("authorization_url"),
                s("client_id"),
                s("client_secret"),
                s("scope"),
                "",
                s("username"),
                s("password"),
                s("callback_url"),
                s("credentials_placement") == "body" ? OAuth2ClientAuthentication.RequestBody : OAuth2ClientAuthentication.BasicHeader,
                s("token_placement") == "query" ? OAuth2TokenPlacement.Query : OAuth2TokenPlacement.Header,
                s("token_header_prefix") is { Length: > 0 } prefix ? prefix : d.HeaderPrefix,
                s("token_query_key") is { Length: > 0 } key ? key : d.QueryParameterName),
        };
    }
}
