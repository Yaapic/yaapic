namespace Yaapic.Core.Export;

/// <summary>Writes an <see cref="ExportSource"/> in one external format.</summary>
/// <remarks>
/// Implementations are pure and deterministic and never throw for content: unconvertible items are skipped and reported.
/// <see cref="ExportException"/> is only for an impossible source, e.g. no collection when one is required.
/// </remarks>
public interface IExporter
{
    /// <summary>Gets the stable identifier.</summary>
    string Id { get; }

    /// <summary>Gets the name shown to the user.</summary>
    string DisplayName { get; }

    /// <summary>Gets the extension of the produced file, with the leading dot.</summary>
    string FileExtension { get; }

    /// <summary>Gets a value indicating whether the source must contain a collection.</summary>
    bool RequiresCollection { get; }

    /// <summary>Exports the source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The files and the report.</returns>
    /// <exception cref="ExportException">The source cannot be exported at all.</exception>
    ExportResult Export(ExportSource source);
}
