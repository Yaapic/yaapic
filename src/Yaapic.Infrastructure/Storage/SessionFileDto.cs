using VYaml.Annotations;

namespace Yaapic.Infrastructure.Storage;

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class SessionFileDto
{
    public int Version { get; set; }

    public List<string>? OpenRequests { get; set; }

    public string? ActiveRequest { get; set; }

    public string? ActiveEnvironment { get; set; }
}
