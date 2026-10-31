namespace Yaapic.Domain.Http;

/// <summary>Content type of a raw request body.</summary>
public enum RawContentType
{
    /// <summary>Plain text.</summary>
    Text,

    /// <summary>JSON.</summary>
    Json,

    /// <summary>XML.</summary>
    Xml,
}
