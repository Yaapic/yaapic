namespace Yaapic.Domain.Http;

/// <summary>A received HTTP response.</summary>
/// <param name="StatusCode">The numeric status code.</param>
/// <param name="ReasonPhrase">The reason phrase.</param>
/// <param name="Headers">The headers in wire order, duplicates kept; all are enabled.</param>
/// <param name="Body">The raw body bytes.</param>
/// <param name="Duration">The time between sending and receiving the response.</param>
/// <param name="FinalUrl">The URL that produced the response.</param>
public sealed record HttpResponseData(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyList<KeyValueEntry> Headers,
    byte[] Body,
    TimeSpan Duration,
    string FinalUrl)
{
    /// <summary>Gets the body size in bytes.</summary>
    public long SizeBytes => Body.LongLength;

    /// <summary>Gets the value of the first <c>Content-Type</c> header (name compared case-insensitively), or <see langword="null"/>.</summary>
    public string? ContentType =>
        Headers.FirstOrDefault(h => string.Equals(h.Key, "Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;

    /// <summary>Gets a value indicating whether the status code is in the 200 to 299 range.</summary>
    public bool IsSuccessStatusCode => StatusCode is >= 200 and <= 299;
}
