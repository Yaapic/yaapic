using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Text helpers used through <c>x:Bind</c> function binding.</summary>
public static class TextMap
{
    /// <summary>Gets <paramref name="text"/> in upper case, as shown in table headers.</summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The upper-case text.</returns>
    public static string Upper(string text) => text.ToUpperInvariant();

    /// <summary>Gets <paramref name="prefix"/> followed by <paramref name="value"/>, as used for accessible names.</summary>
    /// <param name="prefix">The leading text.</param>
    /// <param name="value">The value appended to it.</param>
    /// <returns>The joined text.</returns>
    public static string Prefixed(string prefix, string value) => prefix + value;

    /// <summary>Gets the counts line of an import preview.</summary>
    /// <param name="folders">The folder count.</param>
    /// <param name="requests">The request count.</param>
    /// <param name="environments">The environment count.</param>
    /// <param name="globals">The global variable count.</param>
    /// <returns>The localized counts line.</returns>
    public static string ImportCounts(int folders, int requests, int environments, int globals) => Strings.ImportCounts(folders, requests, environments, globals);

    /// <summary>Gets the "n of m selected" line of the Runner setup.</summary>
    /// <param name="selected">The selected request count.</param>
    /// <param name="total">The request count.</param>
    /// <returns>The localized line.</returns>
    public static string RunnerSelected(int selected, int total) => Strings.RunnerSelectedCount(selected, total);

    /// <summary>Gets the data file summary line of the Runner setup.</summary>
    /// <param name="file">The data file name, or <see langword="null"/> when none.</param>
    /// <param name="rows">The row count.</param>
    /// <param name="columns">The column count.</param>
    /// <returns>The localized line, or an empty string.</returns>
    public static string RunnerData(string? file, int rows, int columns) => file is null ? "" : Strings.RunnerDataSummary(file, rows, columns);

    /// <summary>Gets the "detected format" line of an import preview.</summary>
    /// <param name="format">The detected format name, or <see langword="null"/> when none.</param>
    /// <returns>The localized line, or an empty string.</returns>
    public static string ImportDetected(string? format) => format is null ? "" : Strings.ImportDetected(format);

    /// <summary>Gets the "nothing to export" line shown when a collection is required but none exists.</summary>
    /// <param name="requiresCollection">Whether the selected format needs a collection.</param>
    /// <param name="collectionCount">The number of collections.</param>
    /// <returns>The localized line, or an empty string.</returns>
    public static string ExportEmpty(bool requiresCollection, int collectionCount) =>
        requiresCollection && collectionCount == 0 ? Strings.ExportNoCollections : "";

    /// <summary>Gets the "saved" line shown after an export.</summary>
    /// <param name="savedName">The saved file name, or <see langword="null"/> when nothing was saved.</param>
    /// <returns>The localized line, or an empty string.</returns>
    public static string ExportSaved(string? savedName) => savedName is null ? "" : Strings.ExportSaved(savedName);

    /// <summary>Gets the hint shown for a method whose requests carry no body.</summary>
    /// <param name="methodText">The upper-case method name.</param>
    /// <returns>The hint.</returns>
    public static string NoBodyHint(string methodText) => Strings.RequestNoBodyHint(methodText);

    /// <summary>Gets the code editor language of a raw body content type.</summary>
    /// <param name="contentType">The content type.</param>
    /// <returns><c>json</c>, <c>xml</c> or <c>text</c>.</returns>
    public static string Language(RawContentType contentType) => contentType switch
    {
        RawContentType.Json => "json",
        RawContentType.Xml => "xml",
        RawContentType.Html => "html",
        RawContentType.JavaScript => "javascript",
        _ => "text",
    };

    /// <summary>Gets the code editor language of a script.</summary>
    /// <param name="language">The script language.</param>
    /// <returns><c>javascript</c> or <c>lua</c>.</returns>
    public static string ScriptLanguage(ScriptLanguage language) => language switch
    {
        Domain.Scripting.ScriptLanguage.Lua => "lua",
        _ => "javascript",
    };

    /// <summary>Gets the index of a form field kind in the Text/File picker.</summary>
    /// <param name="kind">The field kind.</param>
    /// <returns>0 for text, 1 for file.</returns>
    public static int KindIndex(FormFieldKind kind) => kind == FormFieldKind.File ? 1 : 0;

    /// <summary>Gets the first line of a list, or an empty string.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The first line, or an empty string.</returns>
    public static string FirstLine(IReadOnlyList<string> lines) => lines.Count > 0 ? lines[0] : "";

    /// <summary>Joins lines with line breaks, as shown in a tooltip.</summary>
    /// <param name="lines">The lines.</param>
    /// <returns>The joined text.</returns>
    public static string JoinLines(IReadOnlyList<string> lines) => string.Join('\n', lines);

    /// <summary>Gets the tooltip of the vault status item.</summary>
    /// <param name="protectorLabel">The protector label; <see langword="null"/> when no vault exists.</param>
    /// <returns>The tooltip text.</returns>
    public static string VaultTip(string? protectorLabel) => protectorLabel ?? Strings.VaultUnprotected;

    /// <summary>Gets the Segoe Fluent glyph of the vault status item.</summary>
    /// <param name="isUnlocked">Whether the vault is unlocked.</param>
    /// <returns>The lock or unlock glyph.</returns>
    public static string VaultGlyph(bool isUnlocked) => isUnlocked ? "" : "";
}
