namespace Yaapic.Domain.Http;

/// <summary>A key/value pair that can be toggled on or off, such as a header or query parameter.</summary>
/// <param name="Key">The entry name.</param>
/// <param name="Value">The entry value.</param>
/// <param name="Enabled">Whether the entry is sent with the request.</param>
/// <param name="IsSecret">Whether the row is a secret variable (its value lives in the vault); only variable lists use it.</param>
public sealed record KeyValueEntry(string Key, string Value, bool Enabled = true, bool IsSecret = false);
