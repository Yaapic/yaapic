namespace Yaapic.Domain.Http;

/// <summary>Text conversion and body rules for <see cref="HttpMethodKind"/>.</summary>
public static class HttpMethodKindExtensions
{
    /// <summary>Gets the upper-case wire name of the method.</summary>
    /// <param name="method">The method.</param>
    /// <returns>The name, for example <c>GET</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="method"/> is not a defined value.</exception>
    public static string ToText(this HttpMethodKind method) => method switch
    {
        HttpMethodKind.Get => "GET",
        HttpMethodKind.Post => "POST",
        HttpMethodKind.Put => "PUT",
        HttpMethodKind.Patch => "PATCH",
        HttpMethodKind.Delete => "DELETE",
        HttpMethodKind.Head => "HEAD",
        HttpMethodKind.Options => "OPTIONS",
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null),
    };

    /// <summary>Parses an upper-case method name; the match is ordinal and the text is not trimmed.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="method">The parsed method when the result is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> names a supported method.</returns>
    public static bool TryParse(string? text, out HttpMethodKind method)
    {
        switch (text)
        {
            case "GET": method = HttpMethodKind.Get; return true;
            case "POST": method = HttpMethodKind.Post; return true;
            case "PUT": method = HttpMethodKind.Put; return true;
            case "PATCH": method = HttpMethodKind.Patch; return true;
            case "DELETE": method = HttpMethodKind.Delete; return true;
            case "HEAD": method = HttpMethodKind.Head; return true;
            case "OPTIONS": method = HttpMethodKind.Options; return true;
            default: method = default; return false;
        }
    }

    /// <summary>Tells whether requests with this method carry a body.</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for POST, PUT, PATCH and DELETE.</returns>
    public static bool AllowsBody(this HttpMethodKind method) =>
        method is HttpMethodKind.Post or HttpMethodKind.Put or HttpMethodKind.Patch or HttpMethodKind.Delete;
}
