using Yaapic.Core.Auth.Aws;
using Yaapic.Core.Auth.Digest;
using Yaapic.Core.Auth.Jwt;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Auth;

/// <summary>Routes the pending authentication of a request to the <see cref="IAuthMethod"/> of its kind.</summary>
public sealed class RequestAuthenticator : IRequestAuthenticator
{
    private readonly Dictionary<AuthKind, IAuthMethod> _methods = [];

    /// <summary>Initializes a new instance of the <see cref="RequestAuthenticator"/> class.</summary>
    /// <param name="methods">The available methods, one per kind.</param>
    /// <exception cref="ArgumentException">Two methods handle the same kind.</exception>
    public RequestAuthenticator(IEnumerable<IAuthMethod> methods)
    {
        ArgumentNullException.ThrowIfNull(methods);
        foreach (var method in methods)
        {
            if (!_methods.TryAdd(method.Kind, method))
            {
                throw new ArgumentException($"Several authentication methods handle {method.Kind}.", nameof(methods));
            }
        }
    }

    /// <summary>Creates the authenticator with the JWT, Digest, AWS Signature v4 and OAuth 2.0 methods.</summary>
    /// <param name="transport">The transport the Digest probe uses.</param>
    /// <param name="tokens">The OAuth 2.0 token service.</param>
    /// <param name="time">The clock.</param>
    /// <returns>The authenticator.</returns>
    public static RequestAuthenticator CreateDefault(IHttpTransport transport, IOAuth2TokenService tokens, TimeProvider time) =>
        new([new JwtAuthMethod(time), new DigestAuthMethod(transport), new AwsV4AuthMethod(time), new OAuth2AuthMethod(tokens)]);

    /// <inheritdoc/>
    public Task<AuthenticationResult> AuthenticateAsync(ResolvedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.PendingAuth is not { } auth)
        {
            return Task.FromResult(AuthenticationResult.Success(request));
        }

        // The method receives the request without PendingAuth, so it can never loop.
        return _methods.TryGetValue(auth.Kind, out var method)
            ? method.ApplyAsync(request with { PendingAuth = null }, auth, ct)
            : Task.FromResult(AuthenticationResult.Fail(Strings.AuthNotAvailable));
    }
}
