using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.OpenApi;

/// <summary>Maps the <c>security</c> requirements of an operation to a Yaapic authentication with placeholder variables.</summary>
internal static class OpenApiSecurity
{
    private static readonly string[] FlowOrder = ["clientCredentials", "authorizationCode", "password"];

    /// <summary>Maps the effective security requirement of an operation.</summary>
    /// <param name="operation">The operation node.</param>
    /// <param name="root">The document root.</param>
    /// <param name="refs">The reference resolver.</param>
    /// <param name="path">The report path of the operation.</param>
    /// <param name="report">The import report.</param>
    /// <param name="variableNames">Receives the placeholder variable names, each added once.</param>
    /// <returns>The authentication, <see cref="RequestAuth.None"/> when there is none or it is unsupported.</returns>
    internal static RequestAuth Map(
        DataNode operation, DataNode root, OpenApiRefResolver refs, string path, ImportReport report, ICollection<string> variableNames)
    {
        var security = operation["security"] is { IsArray: true } own ? own : root["security"] is { IsArray: true } global ? global : null;
        if (security is null || security.Items.Count == 0 || security[0] is not { IsObject: true, Members.Count: > 0 } requirement)
        {
            return RequestAuth.None;
        }

        if (security.Items.Count > 1 || requirement.Members.Count > 1)
        {
            report.AddOnce(ImportSeverity.Info, "", Strings.ImportOpenapiOtherSecurity);
        }

        var (name, scopes) = (requirement.Members[0].Key, requirement.Members[0].Value);
        var scheme = root["components"]?["securitySchemes"]?[name] is { } raw ? refs.Resolve(raw, path) : null;
        if (scheme is not { IsObject: true })
        {
            report.Warning(path, Strings.ImportOpenapiSecurityUnknown(path, name));
            return RequestAuth.None;
        }

        var id = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        string Var(string suffix)
        {
            var variable = id + "_" + suffix;
            if (!variableNames.Contains(variable))
            {
                variableNames.Add(variable);
            }

            return "{{" + variable + "}}";
        }

        RequestAuth Unsupported(string type)
        {
            report.Warning(path, Strings.ImportAuthUnsupported(path, type));
            return RequestAuth.None;
        }

        var type = scheme.GetString("type");
        switch (type)
        {
            case "http":
                var httpScheme = scheme.GetString("scheme") ?? "";
                return httpScheme.ToLowerInvariant() switch
                {
                    "basic" => RequestAuth.None with { Kind = AuthKind.Basic, Username = Var("username"), Password = Var("password") },
                    "digest" => RequestAuth.None with { Kind = AuthKind.Digest, Username = Var("username"), Password = Var("password") },
                    "bearer" => RequestAuth.None with { Kind = AuthKind.Bearer, Token = Var("token") },
                    _ => Unsupported("http " + httpScheme),
                };
            case "apiKey":
                var place = scheme.GetString("in");
                return place is "header" or "query"
                    ? RequestAuth.None with
                    {
                        Kind = AuthKind.ApiKey,
                        Key = scheme.GetString("name") ?? "",
                        Value = Var("key"),
                        Location = place == "query" ? ApiKeyLocation.Query : ApiKeyLocation.Header,
                    }
                    : Unsupported("apiKey in " + (place ?? "cookie"));
            case "oauth2":
                var flows = scheme["flows"];
                var flow = FlowOrder.FirstOrDefault(f => flows?[f] is { IsObject: true });
                if (flow is null)
                {
                    return Unsupported("oauth2 implicit");
                }

                var definition = refs.Resolve(flows![flow]!, path);
                var password = flow == "password";
                var settings = OAuth2Settings.Default with
                {
                    Grant = flow switch
                    {
                        "authorizationCode" => OAuth2Grant.AuthorizationCode,
                        "password" => OAuth2Grant.Password,
                        _ => OAuth2Grant.ClientCredentials,
                    },
                    TokenUrl = definition.GetString("tokenUrl") ?? "",
                    AuthorizationUrl = flow == "authorizationCode" ? definition.GetString("authorizationUrl") ?? "" : "",
                    ClientId = Var("client_id"),
                    ClientSecret = Var("client_secret"),
                    Scope = scopes.IsArray ? string.Join(' ', scopes.Items.Select(s => s.AsString()).OfType<string>()) : "",
                    Username = password ? Var("username") : "",
                    Password = password ? Var("password") : "",
                    RedirectUri = "",
                };
                return RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = settings };
            default:
                return Unsupported(type ?? "unknown");
        }
    }
}
