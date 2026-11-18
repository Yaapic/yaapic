using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Maps <see cref="JwtSettings"/> and <see cref="AwsV4Settings"/> to and from the request file sections.</summary>
internal static class JwtAwsMapper
{
    private static readonly (JwtAlgorithm Value, string Text)[] Algorithms =
    [
        (JwtAlgorithm.Hs256, "HS256"), (JwtAlgorithm.Hs384, "HS384"), (JwtAlgorithm.Hs512, "HS512"),
        (JwtAlgorithm.Rs256, "RS256"), (JwtAlgorithm.Rs384, "RS384"), (JwtAlgorithm.Rs512, "RS512"),
        (JwtAlgorithm.Es256, "ES256"), (JwtAlgorithm.Es384, "ES384"), (JwtAlgorithm.Es512, "ES512"),
    ];

    /// <summary>Maps JWT settings to their DTO.</summary>
    /// <param name="s">The settings.</param>
    /// <returns>The DTO.</returns>
    public static JwtDto ToDto(JwtSettings s) => new()
    {
        Algorithm = Array.Find(Algorithms, a => a.Value == s.Algorithm).Text ?? "HS256",
        Secret = s.Secret,
        SecretIsBase64 = s.SecretIsBase64,
        PrivateKey = s.PrivateKey,
        Payload = s.Payload,
        Headers = s.ExtraHeaders,
        AddIssuedAt = s.AddIssuedAt,
        ExpiresIn = s.ExpiresInSeconds,
        Placement = s.Placement == JwtPlacement.Query ? "query" : "header",
        HeaderPrefix = s.HeaderPrefix,
        QueryParameter = s.QueryParameterName,
    };

    /// <summary>Maps a DTO to JWT settings; a missing or unknown value reads as the default.</summary>
    /// <param name="dto">The DTO, or <see langword="null"/>.</param>
    /// <returns>The settings.</returns>
    public static JwtSettings FromDto(JwtDto? dto)
    {
        var d = JwtSettings.Default;
        if (dto is null)
        {
            return d;
        }

        var algorithm = Array.Find(Algorithms, a => a.Text == dto.Algorithm);
        return new JwtSettings(
            algorithm.Text is null ? d.Algorithm : algorithm.Value,
            dto.Secret ?? d.Secret,
            dto.SecretIsBase64 ?? d.SecretIsBase64,
            dto.PrivateKey ?? d.PrivateKey,
            dto.Payload ?? d.Payload,
            dto.Headers ?? d.ExtraHeaders,
            dto.AddIssuedAt ?? d.AddIssuedAt,
            dto.ExpiresIn ?? d.ExpiresInSeconds,
            dto.Placement == "query" ? JwtPlacement.Query : d.Placement,
            dto.HeaderPrefix ?? d.HeaderPrefix,
            dto.QueryParameter ?? d.QueryParameterName);
    }

    /// <summary>Maps AWS Signature v4 settings to their DTO.</summary>
    /// <param name="s">The settings.</param>
    /// <returns>The DTO.</returns>
    public static AwsV4Dto ToDto(AwsV4Settings s) => new()
    {
        AccessKeyId = s.AccessKeyId,
        SecretAccessKey = s.SecretAccessKey,
        SessionToken = s.SessionToken,
        Region = s.Region,
        Service = s.Service,
    };

    /// <summary>Maps a DTO to AWS Signature v4 settings; a missing value reads as empty.</summary>
    /// <param name="dto">The DTO, or <see langword="null"/>.</param>
    /// <returns>The settings.</returns>
    public static AwsV4Settings FromDto(AwsV4Dto? dto) => dto is null
        ? AwsV4Settings.Default
        : new AwsV4Settings(
            dto.AccessKeyId ?? "", dto.SecretAccessKey ?? "", dto.SessionToken ?? "", dto.Region ?? "", dto.Service ?? "");
}
