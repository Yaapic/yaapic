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
