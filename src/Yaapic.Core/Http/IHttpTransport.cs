using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>Sends a resolved request over the network.</summary>
public interface IHttpTransport
{
    /// <summary>Sends the request. Network problems, timeouts and cancellation are returned as failures, never thrown.</summary>
    /// <param name="request">The resolved request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The response, or a failure.</returns>
    Task<SendResult> SendAsync(ResolvedRequest request, CancellationToken ct);
}
