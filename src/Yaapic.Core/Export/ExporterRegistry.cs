namespace Yaapic.Core.Export;

/// <summary>The known exporters, in registration order.</summary>
/// <param name="exporters">The registered exporters.</param>
public sealed class ExporterRegistry(IEnumerable<IExporter> exporters)
{
    /// <summary>Gets the exporters in registration order.</summary>
    public IReadOnlyList<IExporter> Exporters { get; } = [.. exporters];

    /// <summary>Finds an exporter by identifier.</summary>
    /// <param name="id">The exporter identifier.</param>
    /// <returns>The exporter, or <see langword="null"/> when none has this identifier.</returns>
    public IExporter? Find(string id) => Exporters.FirstOrDefault(e => e.Id == id);
}
