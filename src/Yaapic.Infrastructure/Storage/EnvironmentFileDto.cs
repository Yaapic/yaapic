using VYaml.Annotations;

namespace Yaapic.Infrastructure.Storage;

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class EnvironmentFileDto
{
    public int Version { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public List<KeyValueDto>? Variables { get; set; }
}
