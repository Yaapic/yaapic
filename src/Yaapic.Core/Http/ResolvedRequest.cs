using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>A request with variables substituted, ready for the transport.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Url">The final URL, with the query string appended.</param>
/// <param name="Headers">The enabled headers; <c>Content-Type</c> is moved to <paramref name="BodyContentType"/> when a body is sent.</param>
/// <param name="BodyText">The body text, or null when no body is sent.</param>
/// <param name="BodyContentType">The body content type, or null when no body is sent.</param>
/// <param name="Unresolved">The distinct unresolved variable names, in order of first occurrence.</param>
public sealed record ResolvedRequest(
    HttpMethodKind Method,
    string Url,
    IReadOnlyList<KeyValueEntry> Headers,
    string? BodyText,
    string? BodyContentType,
    IReadOnlyList<string> Unresolved);
