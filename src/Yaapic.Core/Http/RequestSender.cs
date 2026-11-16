using Yaapic.Core.Auth;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Http;

/// <summary>The "send this request now" use case.</summary>
/// <param name="transport">The transport that performs the network call.</param>
/// <param name="authenticator">Applies OAuth 2.0, JWT, Digest and AWS authentication, or null when none is available.</param>
public sealed class RequestSender(IHttpTransport transport, IRequestAuthenticator? authenticator = null)
{
    /// <summary>Resolves the request against the environment and dynamic variables, then sends it.</summary>
    /// <param name="request">The request definition.</param>
    /// <param name="environment">The active environment, or <see langword="null"/> for none.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result and the unresolved variable names; unresolved variables never block the send.</returns>
    public Task<SendReport> SendAsync(HttpRequestDefinition request, EnvironmentDefinition? environment, CancellationToken ct) =>
        SendAsync(request, new VariableScopes(environment, [], []), ct);

    /// <summary>Resolves the request against the environment, collection, globals and dynamic variables, then sends it.</summary>
    /// <param name="request">The request definition.</param>
    /// <param name="scopes">The variable scopes.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result and the unresolved variable names; unresolved variables never block the send.</returns>
    public async Task<SendReport> SendAsync(HttpRequestDefinition request, VariableScopes scopes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(scopes);

        var resolved = RequestResolver.Resolve(request, scopes.CreateSource());

        SendResult result;
        try
        {
            if (resolved.PendingAuth is not null)
            {
                var auth = await AuthenticateAsync(resolved, ct).ConfigureAwait(false);
                if (auth.Request is null)
                {
                    return new(SendResult.Fail(SendFailureKind.AuthenticationFailed, auth.ErrorMessage ?? Strings.AuthNotAvailable), resolved.Unresolved);
                }

                resolved = auth.Request;
            }

            result = await transport.SendAsync(resolved, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = SendResult.Fail(SendFailureKind.Canceled, Strings.RequestCanceled);
        }
        catch (Exception ex)
        {
            result = SendResult.Fail(SendFailureKind.Unknown, ex.Message);
        }

        return new(result, resolved.Unresolved);
    }

    private async Task<AuthenticationResult> AuthenticateAsync(ResolvedRequest resolved, CancellationToken ct)
    {
        if (authenticator is null)
        {
            return AuthenticationResult.Fail(Strings.AuthNotAvailable);
        }

        try
        {
            return await authenticator.AuthenticateAsync(resolved, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return AuthenticationResult.Fail(ex.Message);
        }
    }
}
