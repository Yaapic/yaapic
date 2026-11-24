using System.Text;
using Yaapic.Core.Import;

namespace Yaapic.Core.Export.OpenApi;

/// <summary>Writes a collection as an OpenAPI 3.0 JSON file.</summary>
public sealed class OpenApiJsonExporter : IExporter
{
    /// <inheritdoc />
    public string Id => "openapi-json";

    /// <inheritdoc />
    public string DisplayName => "OpenAPI 3.0 (JSON)";

    /// <inheritdoc />
    public string FileExtension => ".json";

    /// <inheritdoc />
    public bool RequiresCollection => true;

    /// <inheritdoc />
    public ExportResult Export(ExportSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var report = new ImportReport();
        var text = DataNodeJson.Write(OpenApiDocumentBuilder.Build(source, report), indented: true) + "\n";
        var name = ExportPackager.SafeFileName(source.Name) + ".openapi" + FileExtension;
        return new ExportResult([new ExportFile(name, new UTF8Encoding(false).GetBytes(text))], report);
    }
}
