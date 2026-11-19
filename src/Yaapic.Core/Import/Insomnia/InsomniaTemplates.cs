using System.Text.RegularExpressions;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Insomnia;

/// <summary>Converts Insomnia template syntax (<c>{{ _.name }}</c>, <c>{% uuid 'v4' %}</c>) to Yaapic variables.</summary>
internal static partial class InsomniaTemplates
{
    /// <summary>Translates the templates of a string; text without templates is returned as is.</summary>
    /// <param name="text">The Insomnia text.</param>
    /// <param name="path">The report path of the owner of the text.</param>
    /// <param name="report">Receives one warning per distinct unsupported template.</param>
    /// <returns>The text with the supported templates converted.</returns>
    internal static string Translate(string text, string path, ImportReport report)
    {
        if (!text.Contains("{{", StringComparison.Ordinal) && !text.Contains("{%", StringComparison.Ordinal))
        {
            return text;
        }

        return TemplateRegex().Replace(text, m => m.Groups["expr"].Success
            ? Expression(m, m.Groups["expr"].Value.Trim(), path, report)
            : Tag(m, m.Groups["tag"].Value.Trim(), path, report));
    }

    private static string Expression(Match m, string expr, string path, ImportReport report)
    {
        var dotted = DottedRegex().Match(expr);
        if (dotted.Success)
        {
            return "{{" + dotted.Groups[1].Value + "}}";
        }

        var indexed = IndexedRegex().Match(expr);
        if (indexed.Success)
        {
            return "{{" + indexed.Groups[2].Value + "}}";
        }

        report.AddOnce(ImportSeverity.Warning, path, Strings.ImportInsomniaTemplateTag(expr));
        return m.Value;
    }

    private static string Tag(Match m, string tag, string path, ImportReport report)
    {
        var known = KnownTagRegex().Match(tag);
        if (known.Success)
        {
            if (known.Groups["uuid"].Success)
            {
                return "{{$guid}}";
            }

            return known.Groups["fmt"].Value == "unix" ? "{{$timestamp}}" : "{{$isoTimestamp}}";
        }

        var name = tag.Split([' ', '\t', '\r', '\n'], 2)[0];
        report.AddOnce(ImportSeverity.Warning, path, Strings.ImportInsomniaTemplateTag(name));
        return m.Value;
    }

    [GeneratedRegex(@"\{\{(?<expr>.*?)\}\}|\{%(?<tag>.*?)%\}", RegexOptions.Singleline)]
    private static partial Regex TemplateRegex();

    [GeneratedRegex(@"^_\.([^\s\[\]]+)$")]
    private static partial Regex DottedRegex();

    [GeneratedRegex(@"^_\[\s*(['""])(.+?)\1\s*\]$")]
    private static partial Regex IndexedRegex();

    [GeneratedRegex(@"^(?:(?<uuid>uuid\s+['""]v4['""])|now\s+['""](?<fmt>iso-8601|unix)['""](?:\s*,\s*['""]{2})?)$")]
    private static partial Regex KnownTagRegex();
}
