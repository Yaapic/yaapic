using System.Text.RegularExpressions;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Updates;

/// <summary>Picks the section of the release notes written in the user's language.</summary>
/// <remarks>
/// <c>RELEASE_NOTES.md</c> is one file embedded in the packages; each language starts at a line <c>&lt;!-- lang:en --&gt;</c>
/// (or <c>fr</c>). Notes without such lines are shown as they are.
/// </remarks>
public static partial class ReleaseNotes
{
    [GeneratedRegex(@"^[ \t]*<!--\s*lang:(?<code>[A-Za-z-]+)\s*-->[ \t]*\r?$", RegexOptions.Multiline)]
    private static partial Regex Marker();

    /// <summary>Selects the notes of a language.</summary>
    /// <param name="markdown">The notes as embedded in the package; may be empty.</param>
    /// <param name="language">The language of the interface.</param>
    /// <returns>The section of that language; else the English one, else the first one; the whole text when it has no language markers.</returns>
    public static string For(string markdown, UiLanguage language)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var markers = Marker().Matches(markdown);
        if (markers.Count == 0)
        {
            return markdown;
        }

        Dictionary<string, string> sections = new(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < markers.Count; i++)
        {
            var start = markers[i].Index + markers[i].Length;
            var end = i + 1 < markers.Count ? markers[i + 1].Index : markdown.Length;
            sections.TryAdd(markers[i].Groups["code"].Value, markdown[start..end].Trim());
        }

        var code = language == UiLanguage.French ? "fr" : "en";
        return sections.TryGetValue(code, out var picked) ? picked
            : sections.TryGetValue("en", out var english) ? english
            : sections.Values.First();
    }
}
