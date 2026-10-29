namespace Yaapic.Domain.Http;

/// <summary>A transport-level failure; an HTTP error status is not a failure.</summary>
/// <param name="Kind">The category of failure.</param>
/// <param name="Message">A human-readable description.</param>
public sealed record SendFailure(SendFailureKind Kind, string Message);
