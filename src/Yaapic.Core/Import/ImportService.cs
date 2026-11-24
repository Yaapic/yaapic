using Yaapic.Localization;

namespace Yaapic.Core.Import;

/// <summary>An import ready to be applied: the importer that read the input and what it produced.</summary>
/// <param name="Importer">The importer used.</param>
/// <param name="Bundle">The converted data.</param>
public sealed record ImportPreview(IImporter Importer, ImportBundle Bundle);

/// <summary>Detects the format of an input, converts it to a preview and applies the preview.</summary>
/// <param name="registry">The known importers.</param>
/// <param name="applier">Writes a bundle to the stores.</param>
public sealed class ImportService(ImporterRegistry registry, ImportApplier applier)
{
    private const int MaxRequests = 20000;

    /// <summary>Converts an input without writing anything.</summary>
    /// <param name="input">The input to read.</param>
    /// <param name="importerId">The importer to use, or <see langword="null"/> to detect it.</param>
    /// <param name="options">The import options.</param>
    /// <returns>The preview.</returns>
    /// <exception cref="ArgumentException"><paramref name="importerId"/> is unknown.</exception>
    /// <exception cref="ImportException">The format is not recognized, the import failed or is too large.</exception>
    public ImportPreview Preview(ImportInput input, string? importerId, ImportOptions options)
    {
        var importer = importerId is null
            ? registry.Detect(input).FirstOrDefault() ?? throw new ImportException(Strings.ImportFormatNotRecognized)
            : registry.Find(importerId) ?? throw new ArgumentException($"Unknown importer '{importerId}'.", nameof(importerId));

        ImportBundle bundle;
        try
        {
            bundle = importer.Import(input, ImportContext.Create(options));
        }
        catch (Exception ex) when (ex is ImportException or OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ImportException(Strings.ImportFailed(importer.DisplayName, ex.Message), ex);
        }

        if (bundle.RequestCount > MaxRequests)
        {
            throw new ImportException(Strings.ImportTooLarge(bundle.RequestCount));
        }

        if (bundle.RequestCount == 0 && bundle.Environments.Count == 0 && bundle.Globals.Count == 0)
        {
            bundle.Report.Warning("", Strings.ImportNothingFound(importer.DisplayName));
        }

        return new ImportPreview(importer, bundle);
    }

    /// <summary>Writes a preview to the stores.</summary>
    /// <param name="preview">The preview to apply.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The counts and the report.</returns>
    public Task<ImportOutcome> ApplyAsync(ImportPreview preview, CancellationToken ct) => applier.ApplyAsync(preview.Bundle, ct);
}
