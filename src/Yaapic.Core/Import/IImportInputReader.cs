namespace Yaapic.Core.Import;

/// <summary>Reads the file or folder the user chose into an <see cref="ImportInput"/>.</summary>
public interface IImportInputReader
{
    /// <summary>Reads one file.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The input.</returns>
    /// <exception cref="ImportException">The file is missing, over 50 MiB or unreadable.</exception>
    Task<ImportInput> ReadFileAsync(string path, CancellationToken ct);

    /// <summary>Reads the files of a folder.</summary>
    /// <param name="path">The folder path.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The input.</returns>
    /// <exception cref="ImportException">The folder is missing, holds over 5000 files or 200 MiB, or no readable file.</exception>
    Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct);
}
