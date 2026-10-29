namespace Yaapic.Domain.Http;

/// <summary>The outcome of sending a request: either a response or a failure.</summary>
/// <param name="Response">The response, when one was received.</param>
/// <param name="Failure">The failure, when no response was received.</param>
public sealed record SendResult(HttpResponseData? Response, SendFailure? Failure)
{
    /// <summary>Gets a value indicating whether a response was received (HTTP 4xx/5xx included).</summary>
    public bool IsSuccess => Response is not null;

    /// <summary>Creates a successful result.</summary>
    /// <param name="response">The received response.</param>
    /// <returns>A result holding <paramref name="response"/> and no failure.</returns>
    public static SendResult Success(HttpResponseData response) => new(response, null);

    /// <summary>Creates a failed result.</summary>
    /// <param name="kind">The category of failure.</param>
    /// <param name="message">A human-readable description.</param>
    /// <returns>A result holding a failure and no response.</returns>
    public static SendResult Fail(SendFailureKind kind, string message) => new(null, new SendFailure(kind, message));
}
