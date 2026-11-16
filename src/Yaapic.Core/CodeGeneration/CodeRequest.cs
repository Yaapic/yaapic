using Yaapic.Domain.Http;

namespace Yaapic.Core.CodeGeneration;

/// <summary>The request a code generator turns into source text.</summary>
/// <param name="Method">The upper-case method name.</param>
/// <param name="Url">The URL.</param>
/// <param name="Headers">The headers to send.</param>
/// <param name="Body">The body.</param>
/// <param name="AuthNote">A note generators emit as a comment, or null.</param>
public sealed record CodeRequest(
    string Method,
    string Url,
    IReadOnlyList<KeyValueEntry> Headers,
    CodeBody Body,
    string? AuthNote);
