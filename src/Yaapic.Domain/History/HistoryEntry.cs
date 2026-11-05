using Yaapic.Domain.Http;

namespace Yaapic.Domain.History;

/// <summary>The record of one sent request.</summary>
/// <param name="Id">The unique identifier of the entry.</param>
/// <param name="At">The send time, in UTC.</param>
/// <param name="Request">The request exactly as the user sent it, variables unresolved.</param>
/// <param name="StatusCode">The response status code, or <see langword="null"/> when the send failed.</param>
/// <param name="StatusText">The response reason phrase, empty when the send failed.</param>
/// <param name="Duration">The response time, zero when the send failed.</param>
/// <param name="SizeBytes">The response body size, zero when the send failed.</param>
/// <param name="FailureKind">The failure category, or <see langword="null"/> on success.</param>
/// <param name="FailureMessage">The failure description, or <see langword="null"/> on success.</param>
public sealed record HistoryEntry(
    Guid Id,
    DateTimeOffset At,
    HttpRequestDefinition Request,
    int? StatusCode,
    string StatusText,
    TimeSpan Duration,
    long SizeBytes,
    SendFailureKind? FailureKind,
    string? FailureMessage)
{
    /// <summary>Gets a value indicating whether a response was received (HTTP 4xx/5xx included).</summary>
    public bool IsSuccess => StatusCode is not null;

    /// <summary>Creates an entry from a send outcome.</summary>
    /// <param name="request">The request as sent, stored unchanged.</param>
    /// <param name="result">The outcome of the send.</param>
    /// <param name="at">The send time; it is converted to UTC.</param>
    /// <returns>A new entry with a new identifier.</returns>
    public static HistoryEntry From(HttpRequestDefinition request, SendResult result, DateTimeOffset at)
    {
        var utc = at.ToUniversalTime();
        return result.Response is { } r
            ? new(Guid.NewGuid(), utc, request, r.StatusCode, r.ReasonPhrase, r.Duration, r.SizeBytes, null, null)
            : new(Guid.NewGuid(), utc, request, null, "", TimeSpan.Zero, 0, result.Failure?.Kind, result.Failure?.Message);
    }

    /// <summary>Compares two entries by value, the request through its own comparison.</summary>
    /// <param name="other">The entry to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool IsEquivalentTo(HistoryEntry other) =>
        Id == other.Id
        && At == other.At
        && Request.IsEquivalentTo(other.Request)
        && StatusCode == other.StatusCode
        && StatusText == other.StatusText
        && Duration == other.Duration
        && SizeBytes == other.SizeBytes
        && FailureKind == other.FailureKind
        && FailureMessage == other.FailureMessage;
}
