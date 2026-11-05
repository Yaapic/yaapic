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
