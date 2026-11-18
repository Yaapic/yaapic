using Yaapic.Core.Auth;
using Yaapic.Core.Http;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IRequestAuthenticator"/> that records requests and returns a configured result or throws.</summary>
/// <param name="reply">Produces the result for each request; defaults to returning the request unchanged.</param>
public sealed class FakeRequestAuthenticator(Func<ResolvedRequest, CancellationToken, Task<AuthenticationResult>>? reply = null) : IRequestAuthenticator
{
    private readonly List<ResolvedRequest> _requests = [];

    /// <summary>Gets the requests received so far, in order.</summary>
    public IReadOnlyList<ResolvedRequest> Requests => _requests;

    /// <inheritdoc/>
    public Task<AuthenticationResult> AuthenticateAsync(ResolvedRequest request, CancellationToken ct)
    {
        _requests.Add(request);
        return reply is null ? Task.FromResult(AuthenticationResult.Success(request)) : reply(request, ct);
    }
}
