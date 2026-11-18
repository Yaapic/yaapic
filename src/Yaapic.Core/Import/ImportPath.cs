namespace Yaapic.Core.Import;

/// <summary>Builds the source paths shown in the import report.</summary>
public static class ImportPath
{
    /// <summary>Joins path parts with <c>/</c>, dropping empty parts.</summary>
    /// <param name="parts">The parts to join.</param>
    /// <returns>The joined path, or an empty string when no part is left.</returns>
    public static string Join(params ReadOnlySpan<string> parts)
    {
        var kept = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (!string.IsNullOrEmpty(part))
            {
                kept.Add(part);
            }
        }

        return string.Join('/', kept);
    }
}
