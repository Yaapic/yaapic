using System.Text;

namespace Yaapic.Infrastructure.Storage;

/// <summary>File helpers shared by the workspace stores.</summary>
internal static class WorkspaceFiles
{
    private const int MaxSlugLength = 60;

    /// <summary>Builds the file-name slug of an entity name, as described in <c>docs/file-format.md</c>.</summary>
    /// <param name="name">The entity name.</param>
    /// <param name="fallback">The slug used when nothing is left of the name.</param>
    /// <returns>The slug.</returns>
    public static string Slugify(string name, string fallback)
    {
        var sb = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                sb.Append(c);
            }
            else if (sb.Length > 0 && sb[^1] != '-')
            {
                sb.Append('-');
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength].TrimEnd('-');
        }

        return slug.Length == 0 ? fallback : slug;
    }

    /// <summary>Finds a free entry name in a directory: <c>slug+suffix</c>, then <c>slug-2+suffix</c>, <c>-3</c>, ...</summary>
    /// <param name="parentDirectory">The directory that will hold the entry.</param>
    /// <param name="slug">The base name.</param>
    /// <param name="suffix">The suffix, such as <c>.request.yaml</c>; empty for a directory.</param>
    /// <param name="ownPath">An existing entry that may keep its name (for renames), or <see langword="null"/>.</param>
    /// <returns>The free entry name, compared case-insensitively.</returns>
    public static string UniqueName(string parentDirectory, string slug, string suffix, string? ownPath)
    {
        var own = ownPath is null ? null : Path.GetFullPath(ownPath);
        var taken = Directory.Exists(parentDirectory)
            ? Directory.EnumerateFileSystemEntries(parentDirectory)
                .Where(p => !string.Equals(Path.GetFullPath(p), own, StringComparison.OrdinalIgnoreCase))
                .Select(p => Path.GetFileName(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        var name = slug + suffix;
        for (var n = 2; taken.Contains(name); n++)
        {
            name = $"{slug}-{n}{suffix}";
        }

        return name;
    }

    /// <summary>Writes a UTF-8 file (no BOM) through a temporary file, creating the directory if needed.</summary>
    /// <param name="path">The target path.</param>
    /// <param name="content">The file content.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the file is in place.</returns>
    public static async Task WriteAtomicAsync(string path, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content, new UTF8Encoding(false), ct);
        File.Move(tmp, path, overwrite: true);
    }
}
