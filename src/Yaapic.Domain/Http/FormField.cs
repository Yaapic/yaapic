namespace Yaapic.Domain.Http;

/// <summary>A field of a url-encoded or multipart form.</summary>
/// <param name="Key">The field name.</param>
/// <param name="Value">The field value, or the file path when <paramref name="Kind"/> is <see cref="FormFieldKind.File"/>.</param>
/// <param name="Enabled">Whether the field is sent.</param>
/// <param name="Kind">Whether the value is text or a file path.</param>
public sealed record FormField(string Key, string Value, bool Enabled = true, FormFieldKind Kind = FormFieldKind.Text);
