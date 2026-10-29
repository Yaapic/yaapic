namespace Yaapic.Domain.Http;

public sealed record RequestBody(BodyKind Kind, RawContentType ContentType, string Text)
{
    public static RequestBody None { get; } = new(BodyKind.None, RawContentType.Text, "");

    public static RequestBody Raw(RawContentType type, string text) => new(BodyKind.Raw, type, text);
}
