namespace Yaapic.Domain.Http;

/// <summary>The body of a request.</summary>
/// <param name="Kind">The kind of body.</param>
/// <param name="ContentType">The content type used when <paramref name="Kind"/> is <see cref="BodyKind.Raw"/>.</param>
/// <param name="Text">The raw body text.</param>
public sealed record RequestBody(BodyKind Kind, RawContentType ContentType, string Text)
{
    /// <summary>Gets the empty body.</summary>
    public static RequestBody None { get; } = new(BodyKind.None, RawContentType.Text, "");

    /// <summary>Creates a raw body.</summary>
    /// <param name="type">The content type of the text.</param>
    /// <param name="text">The body text.</param>
    /// <returns>A raw <see cref="RequestBody"/>.</returns>
    public static RequestBody Raw(RawContentType type, string text) => new(BodyKind.Raw, type, text);
}
