using VYaml.Annotations;

namespace Yaapic.Infrastructure.Storage;

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class MonitorFileDto
{
    public int Version { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? FolderId { get; set; }

    public string? EnvironmentId { get; set; }

    public int? IntervalMinutes { get; set; }

    public bool? Enabled { get; set; }
}
