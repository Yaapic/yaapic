using System.Text.RegularExpressions;
using Yaapic.Core.Import;
using Yaapic.Localization;

namespace Yaapic.Core.Export.Insomnia;

/// <summary>Converts Yaapic variables (<c>{{name}}</c>, <c>{{$guid}}</c>) to Insomnia template syntax: the inverse of the import.</summary>
internal static partial class InsomniaTemplateWriter
{
    /// <summary>Translates the variables of a string; text without variables is returned as is.</summary>
    /// <param name="text">The Yaapic text.</param>
    /// <param name="path">The report path of the owner of the text.</param>
    /// <param name="report">Receives one warning per distinct dynamic variable that has no Insomnia equivalent.</param>
    /// <returns>The text with the supported variables converted.</returns>
    internal static string Translate(string text, string path, ImportReport report)
    {
        if (!text.Contains("{{", StringComparison.Ordinal))
        {
            return text;
        }

        return VariableRegex().Replace(text, m =>
        {
            var name = m.Groups[1].Value;
            if (name.StartsWith('$'))
            {
                switch (name)
                {
                    case "$guid": return "{% uuid 'v4' %}";
                    case "$isoTimestamp": return "{% now 'iso-8601', '' %}";
                    case "$timestamp": return "{% now 'unix', '' %}";
                    default:
                        report.AddOnce(ImportSeverity.Warning, path, Strings.ExportInsomniaDynamicVariable(name));
                        return m.Value;
                }
            }

            if (name.Length == 0 || name.Contains('\'', StringComparison.Ordinal)) return m.Value;
            return IdentifierRegex().IsMatch(name) ? "{{ _." + name + " }}" : "{{ _['" + name + "'] }}";
        });
    }

    [GeneratedRegex(@"\{\{\s*([^{}]*?)\s*\}\}")]
    private static partial Regex VariableRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex IdentifierRegex();
}
