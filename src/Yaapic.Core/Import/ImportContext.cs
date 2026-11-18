namespace Yaapic.Core.Import;

/// <summary>The services and report shared by one import run.</summary>
/// <param name="newId">Produces identifiers for imported items (replaced by a sequence in tests).</param>
/// <param name="options">The import options.</param>
public sealed class ImportContext(Func<Guid> newId, ImportOptions options)
{
    /// <summary>Gets the import options.</summary>
    public ImportOptions Options { get; } = options;

    /// <summary>Gets the report the importer fills.</summary>
    public ImportReport Report { get; } = new();

    /// <summary>Creates a context that uses <see cref="Guid.NewGuid"/>.</summary>
    /// <param name="options">The options; <see cref="ImportOptions.Default"/> when <see langword="null"/>.</param>
    /// <returns>The context.</returns>
    public static ImportContext Create(ImportOptions? options = null) => new(Guid.NewGuid, options ?? ImportOptions.Default);

    /// <summary>Gets a new identifier.</summary>
    /// <returns>The identifier.</returns>
    public Guid NewId() => newId();
}
