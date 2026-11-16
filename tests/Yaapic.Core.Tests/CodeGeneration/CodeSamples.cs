using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.CodeGeneration;

/// <summary>Sample requests shared by every code generator test.</summary>
public static class CodeSamples
{
    public const string JsonBody = "{\"text\":\"say \\\"hi\\\" C:\\\\dir\\nline2 é\"}";

    public static ResolvedRequest GetSimpleRequest { get; } = Make(
        HttpMethodKind.Get, "https://api.example.com/users?id=1&name=a%20b", [new("Accept", "application/json")]);

    public static ResolvedRequest PostJsonRequest { get; } = Make(
        HttpMethodKind.Post, "https://api.example.com/users", [new("Accept", "application/json")], JsonBody, "application/json");

    public static ResolvedRequest PostFormRequest { get; } = Make(
        HttpMethodKind.Post, "https://api.example.com/form", [], "a=1&b=x y", "application/x-www-form-urlencoded");

    public static ResolvedRequest PostMultipartRequest { get; } = Make(
        HttpMethodKind.Post, "https://api.example.com/upload", [], null, "multipart/form-data",
        parts: [new("title", "Report", null), new("file", null, "/tmp/report.pdf")]);

    public static ResolvedRequest PutFileRequest { get; } = Make(
        HttpMethodKind.Put, "https://api.example.com/blob", [], null, "application/octet-stream", filePath: "/tmp/data.bin");

    public static ResolvedRequest HeadersAwkwardRequest { get; } = Make(
        HttpMethodKind.Get, "https://api.example.com/h", [new("X-Quote", "say \"hi\" it's")]);

    public static ResolvedRequest UnresolvedRequest { get; } = Make(
        HttpMethodKind.Get, "https://{{host}}/users", [], unresolved: ["host"]);

    public static ResolvedRequest DigestNoteRequest { get; } = Make(
        HttpMethodKind.Get, "https://api.example.com/digest", [], auth: new ResolvedAuth(AuthKind.Digest, "u", "p", null, null, null));

    public static CodeRequest GetSimple { get; } = CodeRequestFactory.From(GetSimpleRequest);

    public static CodeRequest PostJson { get; } = CodeRequestFactory.From(PostJsonRequest);

    public static CodeRequest PostForm { get; } = CodeRequestFactory.From(PostFormRequest);

    public static CodeRequest PostMultipart { get; } = CodeRequestFactory.From(PostMultipartRequest);

    public static CodeRequest PutFile { get; } = CodeRequestFactory.From(PutFileRequest);

    public static CodeRequest HeadersAwkward { get; } = CodeRequestFactory.From(HeadersAwkwardRequest);

    public static CodeRequest Unresolved { get; } = CodeRequestFactory.From(UnresolvedRequest);

    public static CodeRequest DigestNote { get; } = CodeRequestFactory.From(DigestNoteRequest);

    public static ResolvedRequest Make(
        HttpMethodKind method,
        string url,
        IReadOnlyList<KeyValueEntry> headers,
        string? body = null,
        string? contentType = null,
        IReadOnlyList<ResolvedPart>? parts = null,
        string? filePath = null,
        ResolvedAuth? auth = null,
        IReadOnlyList<string>? unresolved = null) =>
        new(method, url, headers, body, contentType, unresolved ?? [], parts, filePath, PendingAuth: auth);
}
