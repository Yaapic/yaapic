using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Auth.OAuth2;

/// <summary>Sends the OAuth 2.0 access token in the header or the query string of a request.</summary>
/// <param name="tokens">The token service.</param>
public sealed class OAuth2AuthMethod(IOAuth2TokenService tokens) : IAuthMethod
{
    private const string AuthorizationHeader = "Authorization";

    /// <inheritdoc />
    public AuthKind Kind => AuthKind.OAuth2;

    /// <inheritdoc />
    public async Task<AuthenticationResult> ApplyAsync(ResolvedRequest request, ResolvedAuth auth, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(auth);
        var settings = auth.OAuth2 ?? OAuth2Settings.Default;

        // The user's own header wins, so no token is requested (no needless network call).
        if (settings.Placement == OAuth2TokenPlacement.Header
            && request.Headers.Any(h => h.Key.Equals(AuthorizationHeader, StringComparison.OrdinalIgnoreCase)))
        {
            return AuthenticationResult.Success(request);
        }

        var result = await tokens.GetTokenAsync(settings, ct).ConfigureAwait(false);
        if (result.Token is not { } token)
        {
            return AuthenticationResult.Fail(Strings.Oauth2TokenError + result.Error);
        }

        if (settings.Placement == OAuth2TokenPlacement.Query)
        {
            var name = settings.QueryParameterName.Length == 0 ? "access_token" : settings.QueryParameterName;
            return AuthenticationResult.Success(request.WithQueryParameter(name, token.AccessToken));
        }

        // An empty prefix falls back to the server's token type; with neither, the bare token is sent.
        var prefix = settings.HeaderPrefix.Length > 0 ? settings.HeaderPrefix : token.TokenType;
        var value = prefix.Length > 0 ? $"{prefix} {token.AccessToken}" : token.AccessToken;
        return AuthenticationResult.Success(request.WithHeaderUnlessPresent(AuthorizationHeader, value));
    }
}
