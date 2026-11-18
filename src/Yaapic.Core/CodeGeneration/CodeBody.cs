using Yaapic.Domain.Http;

namespace Yaapic.Core.CodeGeneration;

/// <summary>The body of a <see cref="CodeRequest"/>.</summary>
public abstract record CodeBody
{
    private CodeBody()
    {
    }

    /// <summary>No body.</summary>
    public sealed record None : CodeBody;

    /// <summary>A text body.</summary>
    /// <param name="Content">The body text.</param>
    /// <param name="ContentType">The content type, or null when unknown.</param>
    public sealed record Text(string Content, string? ContentType) : CodeBody;

    /// <summary>A url-encoded form body.</summary>
    /// <param name="Fields">The decoded names and values.</param>
    public sealed record Form(IReadOnlyList<KeyValueEntry> Fields) : CodeBody;

    /// <summary>A multipart form body.</summary>
    /// <param name="Parts">The parts.</param>
    public sealed record Multipart(IReadOnlyList<CodePart> Parts) : CodeBody;

    /// <summary>A file sent as the body.</summary>
    /// <param name="Path">The file path.</param>
    /// <param name="ContentType">The content type, or null when unknown.</param>
    public sealed record File(string Path, string? ContentType) : CodeBody;
}

/// <summary>A part of a multipart code body.</summary>
/// <param name="Name">The part name.</param>
/// <param name="Value">The text value, or null for a file part.</param>
/// <param name="FilePath">The file path, or null for a text part.</param>
public sealed record CodePart(string Name, string? Value, string? FilePath);
