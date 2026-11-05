using VYaml.Annotations;

namespace Yaapic.Infrastructure.Storage;

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class FolderFileDto
{
    public int Version { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public List<KeyValueDto>? Variables { get; set; }

    public ScriptsDto? Scripts { get; set; }
}
