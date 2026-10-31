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
}
