using VYaml.Annotations;

namespace Yaapic.Infrastructure.Storage;

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class GlobalsFileDto
{
    public int Version { get; set; }

    public List<KeyValueDto>? Variables { get; set; }
}
