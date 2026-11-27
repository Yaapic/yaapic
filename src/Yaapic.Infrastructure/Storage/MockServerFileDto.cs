using VYaml.Annotations;

namespace Yaapic.Infrastructure.Storage;

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class MockServerFileDto
{
    public int Version { get; set; }

    public string? Id { get; set; }

    public string? Name { get; set; }

    public int? Port { get; set; }

    public bool? Cors { get; set; }

    public List<MockRouteFileDto>? Routes { get; set; }
}

[YamlObject(NamingConvention.LowerCamelCase)]
internal partial class MockRouteFileDto
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    public bool? Enabled { get; set; }

    public string? Method { get; set; }

    public string? Path { get; set; }

    public int? Status { get; set; }

    public List<KeyValueDto>? Headers { get; set; }

    public ScriptSourceText? Body { get; set; }

    public int? DelayMs { get; set; }
}
