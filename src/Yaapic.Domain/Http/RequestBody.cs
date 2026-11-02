namespace Yaapic.Domain.Http;

/// <summary>The body of a request.</summary>
/// <param name="Kind">The kind of body.</param>
/// <param name="ContentType">The content type used when <paramref name="Kind"/> is <see cref="BodyKind.Raw"/>.</param>
/// <param name="Text">The raw body text.</param>
/// <param name="Fields">The form fields used by <see cref="BodyKind.UrlEncoded"/> and <see cref="BodyKind.FormData"/>.</param>
/// <param name="FilePath">The path of the file sent when <paramref name="Kind"/> is <see cref="BodyKind.Binary"/>.</param>
public sealed record RequestBody(
    BodyKind Kind,
    RawContentType ContentType,
    string Text,
    IReadOnlyList<FormField> Fields,
    string FilePath)
{
    /// <summary>Initializes a body with no form fields and no file.</summary>
    /// <param name="kind">The kind of body.</param>
    /// <param name="contentType">The raw content type.</param>
    /// <param name="text">The raw body text.</param>
    public RequestBody(BodyKind kind, RawContentType contentType, string text)
        : this(kind, contentType, text, [], "")
    {
    }

    /// <summary>Gets the empty body.</summary>
    public static RequestBody None { get; } = new(BodyKind.None, RawContentType.Text, "");

    /// <summary>Creates a raw body.</summary>
    /// <param name="type">The content type of the text.</param>
    /// <param name="text">The body text.</param>
    /// <returns>A raw <see cref="RequestBody"/>.</returns>
    public static RequestBody Raw(RawContentType type, string text) => new(BodyKind.Raw, type, text);

    /// <summary>Creates a url-encoded form body.</summary>
    /// <param name="fields">The form fields.</param>
    /// <returns>A url-encoded <see cref="RequestBody"/>.</returns>
    public static RequestBody UrlEncoded(IReadOnlyList<FormField> fields) =>
        new(BodyKind.UrlEncoded, RawContentType.Text, "", fields, "");

    /// <summary>Creates a multipart form body.</summary>
    /// <param name="fields">The form fields.</param>
    /// <returns>A multipart <see cref="RequestBody"/>.</returns>
    public static RequestBody FormData(IReadOnlyList<FormField> fields) =>
        new(BodyKind.FormData, RawContentType.Text, "", fields, "");

    /// <summary>Creates a body that sends the content of a file.</summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>A binary <see cref="RequestBody"/>.</returns>
    public static RequestBody Binary(string path) => new(BodyKind.Binary, RawContentType.Text, "", [], path);

    /// <summary>Compares two bodies by value, including the contents of <see cref="Fields"/>.</summary>
    /// <param name="other">The body to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool Equals(RequestBody? other) =>
        other is not null
        && Kind == other.Kind
        && ContentType == other.ContentType
        && Text == other.Text
        && FilePath == other.FilePath
        && Fields.SequenceEqual(other.Fields);

    /// <summary>Gets a hash code consistent with <see cref="Equals(RequestBody?)"/>.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);
        hash.Add(ContentType);
        hash.Add(Text);
        hash.Add(FilePath);
        foreach (var field in Fields)
        {
            hash.Add(field);
        }

        return hash.ToHashCode();
    }
}
