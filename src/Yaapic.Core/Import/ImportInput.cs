namespace Yaapic.Core.Import;

/// <summary>What the user chose to import: a single file or the files of a folder.</summary>
/// <param name="Name">The file or folder name without extension (used to name HAR and cURL collections).</param>
/// <param name="Files">The files, relative to the chosen root.</param>
public sealed record ImportInput(string Name, IReadOnlyList<ImportFile> Files)
{
    /// <summary>Gets the first file.</summary>
    /// <exception cref="InvalidOperationException">There is no file.</exception>
    public ImportFile Single => Files.Count > 0 ? Files[0] : throw new InvalidOperationException("The import input has no file.");

    /// <summary>Creates an input made of one file.</summary>
    /// <param name="name">The file name, also used as the input name.</param>
    /// <param name="text">The file content.</param>
    /// <returns>The input.</returns>
    public static ImportInput FromText(string name, string text) => new(name, [new ImportFile(name, text)]);
}
