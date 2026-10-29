using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IHttpTransport"/> that records requests and answers through a delegate.</summary>
/// <param name="reply">Produces the result for each request.</param>
public sealed class FakeHttpTransport(Func<ResolvedRequest, CancellationToken, Task<SendResult>> reply) : IHttpTransport
{
    private readonly List<ResolvedRequest> _requests = [];

    /// <summary>Gets the requests received so far, in order.</summary>
    public IReadOnlyList<ResolvedRequest> Requests => _requests;

    /// <inheritdoc/>
    public Task<SendResult> SendAsync(ResolvedRequest request, CancellationToken ct)
    {
        _requests.Add(request);
        return reply(request, ct);
    }
}
