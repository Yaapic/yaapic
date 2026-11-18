using Yaapic.Core.Responses;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Diff;

/// <summary>A response kept for comparison: status, headers and formatted body text.</summary>
/// <param name="StatusCode">The status code.</param>
/// <param name="ReasonPhrase">The reason phrase.</param>
/// <param name="Headers">The headers in wire order.</param>
/// <param name="BodyText">The body as the response panel shows it (pretty form).</param>
/// <param name="ContentType">The <c>Content-Type</c> header value, if any.</param>
/// <param name="At">When the response was received.</param>
public sealed record ResponseSnapshot(
    int StatusCode,
    string ReasonPhrase,
    IReadOnlyList<KeyValueEntry> Headers,
    string BodyText,
    string? ContentType,
    DateTimeOffset At)
{
    /// <summary>The largest body, in bytes, that can be snapshotted.</summary>
    public const int MaxBodyBytes = 1024 * 1024;

    /// <summary>Creates a snapshot of a response.</summary>
    /// <param name="response">The response.</param>
    /// <param name="at">When it was received.</param>
    /// <returns>The snapshot, or <see langword="null"/> when the body is binary or larger than <see cref="MaxBodyBytes"/>.</returns>
    public static ResponseSnapshot? TryCreate(HttpResponseData response, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.Body.Length > MaxBodyBytes)
        {
            return null;
        }

        var body = ResponseFormatter.Format(response, BodyViewMode.Pretty);
        return body.IsBinary
            ? null
            : new ResponseSnapshot(
                response.StatusCode, response.ReasonPhrase, response.Headers, body.Text, response.ContentType, at);
    }
}
