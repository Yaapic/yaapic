using VYaml.Annotations;

namespace Yaapic.Infrastructure.Storage;

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class RequestFileDto
{
    public int Version { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Method { get; set; }

    public string? Url { get; set; }

    public List<KeyValueDto>? Params { get; set; }

    public List<KeyValueDto>? Headers { get; set; }

    public BodyDto? Body { get; set; }

    public AuthDto? Auth { get; set; }

    public SettingsDto? Settings { get; set; }

    public ScriptsDto? Scripts { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class KeyValueDto
{
    public string? Key { get; set; }

    public string? Value { get; set; }

    public bool? Enabled { get; set; }

    public bool? Secret { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class BodyDto
{
    public string? Type { get; set; }

    public string? ContentType { get; set; }

    public string? Text { get; set; }

    public List<FormFieldDto>? Fields { get; set; }

    public string? File { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class FormFieldDto
{
    public string? Key { get; set; }

    public string? Value { get; set; }

    public bool? Enabled { get; set; }

    public string? Kind { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class AuthDto
{
    public string? Type { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Token { get; set; }

    public string? Key { get; set; }

    public string? Value { get; set; }

    [YamlMember("in")]
    public string? Location { get; set; }

    public OAuth2Dto? Oauth2 { get; set; }

    public JwtDto? Jwt { get; set; }

    public AwsV4Dto? Awsv4 { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class JwtDto
{
    public string? Algorithm { get; set; }

    public string? Secret { get; set; }

    public bool? SecretIsBase64 { get; set; }

    public string? PrivateKey { get; set; }

    public string? Payload { get; set; }

    public string? Headers { get; set; }

    public bool? AddIssuedAt { get; set; }

    public int? ExpiresIn { get; set; }

    public string? Placement { get; set; }

    public string? HeaderPrefix { get; set; }

    public string? QueryParameter { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class AwsV4Dto
{
    public string? AccessKeyId { get; set; }

    public string? SecretAccessKey { get; set; }

    public string? SessionToken { get; set; }

    public string? Region { get; set; }

    public string? Service { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class OAuth2Dto
{
    public string? Grant { get; set; }

    public string? TokenUrl { get; set; }

    public string? AuthorizationUrl { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string? Scope { get; set; }

    public string? Audience { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? RedirectUri { get; set; }

    public string? ClientAuth { get; set; }

    public string? Placement { get; set; }

    public string? HeaderPrefix { get; set; }

    public string? QueryParameter { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class SettingsDto
{
    public bool? FollowRedirects { get; set; }

    public int? TimeoutSeconds { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class ScriptsDto
{
    public ScriptBlockDto? PreRequest { get; set; }

    public ScriptBlockDto? PostResponse { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class ScriptBlockDto
{
    public string? Language { get; set; }

    public ScriptSourceText? Source { get; set; }
}
