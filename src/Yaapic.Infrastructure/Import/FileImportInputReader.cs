using System.Text;
using Yaapic.Core.Import;
using Yaapic.Localization;

namespace Yaapic.Infrastructure.Import;

/// <summary>Reads a file or a folder from disk into an <see cref="ImportInput"/>, enforcing the size limits.</summary>
public sealed class FileImportInputReader : IImportInputReader
{
    private const long MaxFileBytes = 50L * 1024 * 1024;
    private const long MaxFolderBytes = 200L * 1024 * 1024;
    private const int MaxFolderFiles = 5000;

    private static readonly string[] Extensions = [".bru", ".json", ".yml", ".yaml"];

    // Invalid bytes become U+FFFD instead of throwing; the BOM is dropped by StreamReader detection.
    private static readonly UTF8Encoding Utf8 = new(false, false);

    /// <inheritdoc />
    public async Task<ImportInput> ReadFileAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ct.ThrowIfCancellationRequested();
        if (!File.Exists(path))
        {
            throw new ImportException(Strings.ImportFileNotFound(path));
        }

        try
        {
            if (new FileInfo(path).Length > MaxFileBytes)
            {
                throw new ImportException(Strings.ImportFileTooLarge(path));
            }

            var text = await ReadTextAsync(path, ct);
            return new ImportInput(Path.GetFileNameWithoutExtension(path), [new ImportFile(Path.GetFileName(path), text)]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImportException(Strings.ImportFileUnreadable(path, ex.Message), ex);
        }
    }

    /// <inheritdoc />
    public async Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ct.ThrowIfCancellationRequested();
        if (!Directory.Exists(path))
        {
            throw new ImportException(Strings.ImportFileNotFound(path));
        }

        var root = Path.GetFullPath(path);
        List<(string Relative, string Full)> candidates = [];
        try
        {
            Collect(root, "", candidates, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImportException(Strings.ImportFileUnreadable(path, ex.Message), ex);
        }

        candidates.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Relative, b.Relative));

        List<ImportFile> files = [];
        long total = 0;
        foreach (var (relative, full) in candidates)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var length = new FileInfo(full).Length;
                if (length > MaxFileBytes)
                {
                    continue;
                }

                var text = await ReadTextAsync(full, ct);
                total += length;
                if (files.Count + 1 > MaxFolderFiles || total > MaxFolderBytes)
                {
                    throw new ImportException(Strings.ImportFolderTooLarge);
                }

                files.Add(new ImportFile(relative, text));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // One unreadable file does not fail the folder.
            }
        }

        if (files.Count == 0)
        {
            throw new ImportException(Strings.ImportFolderEmpty(path));
        }

        return new ImportInput(Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), files);
    }

    private static void Collect(string directory, string prefix, List<(string Relative, string Full)> into, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0 || entry.LinkTarget is not null)
            {
                continue;
            }

            if (entry is DirectoryInfo)
            {
                if (entry.Name is "node_modules" || entry.Name.StartsWith('.'))
                {
                    continue;
                }

                Collect(entry.FullName, prefix + entry.Name + "/", into, ct);
            }
            else if (Extensions.Contains(entry.Extension, StringComparer.OrdinalIgnoreCase))
            {
                into.Add((prefix + entry.Name, entry.FullName));
                if (into.Count > MaxFolderFiles)
                {
                    throw new ImportException(Strings.ImportFolderTooLarge);
                }
            }
        }
    }

    private static async Task<string> ReadTextAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: false);
        var text = await reader.ReadToEndAsync(ct);
        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }
}
