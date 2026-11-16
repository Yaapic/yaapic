using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>A request with variables substituted, ready for the transport.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Url">The final URL, with the query string appended.</param>
/// <param name="Headers">The enabled headers; <c>Content-Type</c> is moved to <paramref name="BodyContentType"/> when a body is sent.</param>
/// <param name="BodyText">The body text, or null when no body is sent.</param>
/// <param name="BodyContentType">The body content type, or null when no body is sent.</param>
/// <param name="Unresolved">The distinct unresolved variable names, in order of first occurrence.</param>
/// <param name="Parts">The multipart form parts, or null when the body is not multipart.</param>
/// <param name="BodyFilePath">The path of the file sent as the body, or null when the body is not binary.</param>
/// <param name="FollowRedirects">Whether redirects are followed.</param>
/// <param name="TimeoutSeconds">The timeout in seconds; 0 means the application default.</param>
/// <param name="PendingAuth">The OAuth 2.0, JWT, Digest or AWS authentication the sender applies, or null for the other kinds.</param>
/// <param name="HttpVersion">The HTTP version the request asks for.</param>
public sealed record ResolvedRequest(
    HttpMethodKind Method,
    string Url,
    IReadOnlyList<KeyValueEntry> Headers,
    string? BodyText,
    string? BodyContentType,
    IReadOnlyList<string> Unresolved,
    IReadOnlyList<ResolvedPart>? Parts = null,
    string? BodyFilePath = null,
    bool FollowRedirects = true,
    int TimeoutSeconds = 0,
    ResolvedAuth? PendingAuth = null,
    HttpVersionPreference HttpVersion = HttpVersionPreference.Default);

/// <summary>A part of a multipart form body.</summary>
/// <param name="Name">The part name.</param>
/// <param name="Value">The text value, or null for a file part.</param>
/// <param name="FilePath">The path of the file, or null for a text part.</param>
public sealed record ResolvedPart(string Name, string? Value, string? FilePath);
