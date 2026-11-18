namespace Yaapic.Domain.Http;

/// <summary>The OAuth 2.0 grant used to obtain an access token.</summary>
public enum OAuth2Grant
{
    /// <summary>Client credentials grant.</summary>
    ClientCredentials,

    /// <summary>Authorization code grant (browser sign-in).</summary>
    AuthorizationCode,

    /// <summary>Resource owner password grant.</summary>
    Password,
}
