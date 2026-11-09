using Yaapic.Core.Variables;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>What <see cref="ScriptedRequestSender"/> needs to send one request with its scripts.</summary>
/// <param name="Request">The request definition, saved or not.</param>
/// <param name="Scopes">The variable scopes at the start of the send.</param>
/// <param name="Chain">The scripts to run; it already contains the request step (see <see cref="ScriptChain.WithRequest"/>).</param>
/// <param name="HasEnvironment">Whether an environment is active, so that scripts can write to it.</param>
/// <param name="HasCollection">Whether the request belongs to a collection, so that scripts can write to it.</param>
/// <param name="Limits">The script limits; <see langword="null"/> means <see cref="ScriptLimits.Default"/>.</param>
public sealed record ScriptedSendInput(
    HttpRequestDefinition Request,
    VariableScopes Scopes,
    ScriptChain Chain,
    bool HasEnvironment,
    bool HasCollection,
    ScriptLimits? Limits = null);
