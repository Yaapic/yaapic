namespace Yaapic.Infrastructure.Mocks;

/// <summary>The limits applied while reading an HTTP/1.1 request (ADR 0020).</summary>
/// <param name="MaxHeaderBytes">The maximum size of the request line and headers together.</param>
/// <param name="MaxHeaderCount">The maximum number of header lines.</param>
/// <param name="MaxBodyBytes">The maximum body size.</param>
/// <param name="ReadTimeout">The time allowed for each read of a request.</param>
internal sealed record Http11Limits(int MaxHeaderBytes, int MaxHeaderCount, int MaxBodyBytes, TimeSpan ReadTimeout)
{
    /// <summary>Gets the limits of ADR 0020.</summary>
    public static Http11Limits Default { get; } = new(65_536, 100, 10_485_760, TimeSpan.FromSeconds(30));
}
