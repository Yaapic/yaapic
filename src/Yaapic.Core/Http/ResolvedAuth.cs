using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>The advanced authentication of a request with variables substituted, signed or fetched by the sender.</summary>
/// <param name="Kind">The authentication kind: OAuth2, Jwt, Digest or AwsV4.</param>
/// <param name="Username">The Digest username; empty for the other kinds.</param>
/// <param name="Password">The Digest password; empty for the other kinds.</param>
/// <param name="OAuth2">The OAuth 2.0 settings, set when <paramref name="Kind"/> is OAuth2.</param>
/// <param name="Jwt">The JWT settings, set when <paramref name="Kind"/> is Jwt.</param>
/// <param name="AwsV4">The AWS Signature v4 settings, set when <paramref name="Kind"/> is AwsV4.</param>
public sealed record ResolvedAuth(
    AuthKind Kind,
    string Username,
    string Password,
    OAuth2Settings? OAuth2,
    JwtSettings? Jwt,
    AwsV4Settings? AwsV4);
