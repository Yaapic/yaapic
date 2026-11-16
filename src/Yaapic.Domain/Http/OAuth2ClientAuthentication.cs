namespace Yaapic.Domain.Http;

/// <summary>How the client credentials are sent to the token endpoint.</summary>
public enum OAuth2ClientAuthentication
{
    /// <summary>HTTP Basic Authorization header.</summary>
    BasicHeader,

    /// <summary>Form fields in the request body.</summary>
    RequestBody,
}
