namespace Yaapic.Core.CodeGeneration;

/// <summary>Text helpers shared by the generators.</summary>
internal static class CodeText
{
    /// <summary>Splits text into lines on any line ending.</summary>
    internal static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');

    /// <summary>Joins lines with a line feed and a trailing newline.</summary>
    internal static string JoinLines(IEnumerable<string> lines) => string.Join('\n', lines) + "\n";

    /// <summary>Tells whether a content type is JSON (<c>application/json</c> or a <c>+json</c> suffix).</summary>
    internal static bool IsJson(string? contentType)
    {
        if (contentType is null)
        {
            return false;
        }

        var main = contentType.Split(';')[0].Trim();
        return main.Equals("application/json", StringComparison.OrdinalIgnoreCase)
            || main.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
    }
}
