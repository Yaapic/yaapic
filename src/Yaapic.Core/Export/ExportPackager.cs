using System.IO.Compression;
using System.Text;

namespace Yaapic.Core.Export;

/// <summary>Turns the files of an export into the single file the user saves.</summary>
public static class ExportPackager
{
    private const int MaxSlugLength = 60;
    private static readonly DateTimeOffset FixedTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Packages an export: one file as is, several files as a zip.</summary>
    /// <param name="result">The export result.</param>
    /// <param name="baseName">The name of the zip, without extension; made file-system safe.</param>
    /// <param name="extension">The extension of the exporter; unused, a single file keeps its own path.</param>
    /// <returns>The file name and its content.</returns>
    /// <exception cref="InvalidOperationException">The result has no file.</exception>
    /// <exception cref="ArgumentException">A path is rooted, contains <c>..</c> or a backslash.</exception>
    public static (string FileName, byte[] Content) Package(ExportResult result, string baseName, string extension)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(extension);
        var files = result.Files;
        if (files.Count == 0) throw new InvalidOperationException("The export produced no file.");
        foreach (var file in files) CheckPath(file.Path);
        if (files.Count == 1) return (files[0].Path, files[0].Content);

        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                var entry = zip.CreateEntry(file.Path, CompressionLevel.Optimal);
                entry.LastWriteTime = FixedTime;
                using var entryStream = entry.Open();
                entryStream.Write(file.Content);
            }
        }

        return (SafeFileName(baseName) + ".zip", stream.ToArray());
    }

    /// <summary>Builds a file-system safe name: lowercase, other characters collapsed to <c>-</c>, at most 60 characters.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The safe name, or <c>export</c> when nothing is left.</returns>
    public static string SafeFileName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9')) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length > MaxSlugLength) slug = slug[..MaxSlugLength].TrimEnd('-');
        return slug.Length == 0 ? "export" : slug;
    }

    /// <summary>Returns a path not yet used, appending <c>-2</c>, <c>-3</c> before the extension on a clash, and records it.</summary>
    /// <param name="path">The wanted path.</param>
    /// <param name="used">The paths already taken; the result is added to it.</param>
    /// <returns>The free path; clashes are detected ignoring case.</returns>
    public static string UniquePath(string path, ISet<string> used)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(used);
        var dot = path.LastIndexOf('.');
        var slash = path.LastIndexOf('/');
        var (stem, ext) = dot > slash + 1 ? (path[..dot], path[dot..]) : (path, "");
        var candidate = path;
        for (var n = 2; used.Any(u => string.Equals(u, candidate, StringComparison.OrdinalIgnoreCase)); n++)
        {
            candidate = $"{stem}-{n}{ext}";
        }

        used.Add(candidate);
        return candidate;
    }

    private static void CheckPath(string path)
    {
        if (path.Length == 0 || path[0] == '/' || path.Contains('\\') || Path.IsPathRooted(path)
            || path.Split('/').Contains(".."))
        {
            throw new ArgumentException($"Unsafe export path: {path}", nameof(path));
        }
    }
}
