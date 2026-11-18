namespace Yaapic.Domain.Http;

/// <summary>The JWT signing configuration of a request.</summary>
/// <param name="Algorithm">The signing algorithm.</param>
/// <param name="Secret">The HMAC secret (HS* algorithms).</param>
/// <param name="SecretIsBase64">Whether <paramref name="Secret"/> is Base64 encoded.</param>
/// <param name="PrivateKey">The PEM private key (RS* and ES* algorithms).</param>
/// <param name="Payload">The JSON payload.</param>
/// <param name="ExtraHeaders">The extra JWT header fields, as JSON.</param>
/// <param name="AddIssuedAt">Whether the <c>iat</c> claim is added.</param>
/// <param name="ExpiresInSeconds">The lifetime in seconds used for the <c>exp</c> claim; 0 means no expiry.</param>
/// <param name="Placement">Where the token is sent.</param>
/// <param name="HeaderPrefix">The Authorization header prefix.</param>
/// <param name="QueryParameterName">The query parameter name when the token goes in the query string.</param>
public sealed record JwtSettings(
    JwtAlgorithm Algorithm,
    string Secret,
    bool SecretIsBase64,
    string PrivateKey,
    string Payload,
    string ExtraHeaders,
    bool AddIssuedAt,
    int ExpiresInSeconds,
    JwtPlacement Placement,
    string HeaderPrefix,
    string QueryParameterName)
{
    /// <summary>The defaults of a new JWT configuration.</summary>
    public static JwtSettings Default { get; } = new(
        JwtAlgorithm.Hs256, "", false, "", "{}", "", true, 0, JwtPlacement.Header, "Bearer", "token");
}
