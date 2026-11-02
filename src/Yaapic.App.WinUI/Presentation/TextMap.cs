using Yaapic.Domain.Http;

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

    /// <summary>Gets the hint shown for a method whose requests carry no body.</summary>
    /// <param name="methodText">The upper-case method name.</param>
    /// <returns>The hint.</returns>
    public static string NoBodyHint(string methodText) => $"{methodText} requests are sent without a body";

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

    /// <summary>Gets the index of a form field kind in the Text/File picker.</summary>
    /// <param name="kind">The field kind.</param>
    /// <returns>0 for text, 1 for file.</returns>
    public static int KindIndex(FormFieldKind kind) => kind == FormFieldKind.File ? 1 : 0;
}
