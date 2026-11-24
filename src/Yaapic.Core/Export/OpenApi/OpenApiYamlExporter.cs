using System.Text;
using Yaapic.Core.Import;

namespace Yaapic.Core.Export.OpenApi;

/// <summary>Writes a collection as an OpenAPI 3.0 YAML file.</summary>
public sealed class OpenApiYamlExporter : IExporter
{
    /// <inheritdoc />
    public string Id => "openapi-yaml";

    /// <inheritdoc />
    public string DisplayName => "OpenAPI 3.0 (YAML)";

    /// <inheritdoc />
    public string FileExtension => ".yaml";

    /// <inheritdoc />
    public bool RequiresCollection => true;

    /// <inheritdoc />
    public ExportResult Export(ExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var report = new ImportReport();
        var text = DataNodeYaml.Write(OpenApiDocumentBuilder.Build(source, report));
        var name = ExportPackager.SafeFileName(source.Name) + ".openapi" + FileExtension;
        return new ExportResult([new ExportFile(name, new UTF8Encoding(false).GetBytes(text))], report);
    }
}
