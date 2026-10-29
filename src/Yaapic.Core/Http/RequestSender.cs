using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>The "send this request now" use case.</summary>
/// <param name="transport">The transport that performs the network call.</param>
public sealed class RequestSender(IHttpTransport transport)
{
    /// <summary>Resolves the request against the environment and dynamic variables, then sends it.</summary>
    /// <param name="request">The request definition.</param>
    /// <param name="environment">The active environment, or <see langword="null"/> for none.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The result and the unresolved variable names; unresolved variables never block the send.</returns>
    public async Task<SendReport> SendAsync(HttpRequestDefinition request, EnvironmentDefinition? environment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = new CompositeVariableSource(new EnvironmentVariableSource(environment), new DynamicVariableSource());
        var resolved = RequestResolver.Resolve(request, source);

        SendResult result;
        try
        {
            result = await transport.SendAsync(resolved, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            result = SendResult.Fail(SendFailureKind.Canceled, "Request canceled");
        }
        catch (Exception ex)
        {
            result = SendResult.Fail(SendFailureKind.Unknown, ex.Message);
        }

        return new(result, resolved.Unresolved);
    }
}
