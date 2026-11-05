namespace Yaapic.Domain.Http;

/// <summary>Kind of value held by a <see cref="FormField"/>.</summary>
public enum FormFieldKind
{
    /// <summary>The value is text.</summary>
    Text,

    /// <summary>The value is the path of a file to upload.</summary>
    File,
}
