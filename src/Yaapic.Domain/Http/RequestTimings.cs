namespace Yaapic.Domain.Http;

/// <summary>The timing phases of one request.</summary>
/// <param name="Dns">The DNS lookup time, or <see langword="null"/> when not measured or not needed.</param>
/// <param name="Connect">The TCP connect time, or <see langword="null"/> when not measured or the connection was reused.</param>
/// <param name="Tls">The TLS handshake time, or <see langword="null"/> when not measured or not applicable.</param>
/// <param name="Wait">The time between the request being sent and the first response byte.</param>
/// <param name="Download">The time spent receiving the response body.</param>
/// <param name="ConnectionReused">Whether an existing connection served the request.</param>
public sealed record RequestTimings(
    TimeSpan? Dns,
    TimeSpan? Connect,
    TimeSpan? Tls,
    TimeSpan Wait,
    TimeSpan Download,
    bool ConnectionReused)
{
    /// <summary>Gets the timings with every phase at zero and no connection phase measured.</summary>
    public static RequestTimings Empty { get; } = new(null, null, null, TimeSpan.Zero, TimeSpan.Zero, false);

    /// <summary>Gets the sum of every non-null phase, the measured part of the total.</summary>
    public TimeSpan Sum => (Dns ?? TimeSpan.Zero) + (Connect ?? TimeSpan.Zero) + (Tls ?? TimeSpan.Zero) + Wait + Download;
}
