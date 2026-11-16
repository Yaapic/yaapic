using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Maps <see cref="OAuth2Settings"/> to and from the <c>auth.oauth2</c> section of a request file.</summary>
internal static class OAuth2Mapper
{
    /// <summary>Maps settings to their DTO.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The DTO.</returns>
    public static OAuth2Dto ToDto(OAuth2Settings settings) => new()
    {
        Grant = settings.Grant switch
        {
            OAuth2Grant.AuthorizationCode => "authorization_code",
            OAuth2Grant.Password => "password",
            _ => "client_credentials",
        },
        TokenUrl = settings.TokenUrl,
        AuthorizationUrl = settings.AuthorizationUrl,
        ClientId = settings.ClientId,
        ClientSecret = settings.ClientSecret,
        Scope = settings.Scope,
        Audience = settings.Audience,
        Username = settings.Username,
        Password = settings.Password,
        RedirectUri = settings.RedirectUri,
        ClientAuth = settings.ClientAuthentication == OAuth2ClientAuthentication.RequestBody ? "body" : "basic",
        Placement = settings.Placement == OAuth2TokenPlacement.Query ? "query" : "header",
        HeaderPrefix = settings.HeaderPrefix,
        QueryParameter = settings.QueryParameterName,
    };

    /// <summary>Maps a DTO to settings; a missing or unknown value reads as the default.</summary>
    /// <param name="dto">The DTO, or <see langword="null"/>.</param>
    /// <returns>The settings.</returns>
    public static OAuth2Settings FromDto(OAuth2Dto? dto)
    {
        var d = OAuth2Settings.Default;
        if (dto is null)
        {
            return d;
        }

        return new OAuth2Settings(
            dto.Grant switch
            {
                "authorization_code" => OAuth2Grant.AuthorizationCode,
                "password" => OAuth2Grant.Password,
                _ => d.Grant,
            },
            dto.TokenUrl ?? d.TokenUrl,
            dto.AuthorizationUrl ?? d.AuthorizationUrl,
            dto.ClientId ?? d.ClientId,
            dto.ClientSecret ?? d.ClientSecret,
            dto.Scope ?? d.Scope,
            dto.Audience ?? d.Audience,
            dto.Username ?? d.Username,
            dto.Password ?? d.Password,
            dto.RedirectUri ?? d.RedirectUri,
            dto.ClientAuth switch
            {
                "body" => OAuth2ClientAuthentication.RequestBody,
                _ => d.ClientAuthentication,
            },
            dto.Placement switch
            {
                "query" => OAuth2TokenPlacement.Query,
                _ => d.Placement,
            },
            dto.HeaderPrefix ?? d.HeaderPrefix,
            dto.QueryParameter ?? d.QueryParameterName);
    }
}
