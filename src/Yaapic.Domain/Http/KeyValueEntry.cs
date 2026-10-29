namespace Yaapic.Domain.Http;

/// <summary>A key/value pair that can be toggled on or off, such as a header or query parameter.</summary>
/// <param name="Key">The entry name.</param>
/// <param name="Value">The entry value.</param>
/// <param name="Enabled">Whether the entry is sent with the request.</param>
public sealed record KeyValueEntry(string Key, string Value, bool Enabled = true);
