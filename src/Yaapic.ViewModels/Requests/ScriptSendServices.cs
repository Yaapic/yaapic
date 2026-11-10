using Yaapic.Core.Scripting;
using Yaapic.ViewModels.Console;

namespace Yaapic.ViewModels.Requests;

/// <summary>What a request editor needs to send through the script pipeline.</summary>
/// <param name="Sender">Runs the scripts around the send.</param>
/// <param name="Chains">Gives the scripts inherited from the collection and folders.</param>
/// <param name="Console">Receives the script output and network lines.</param>
/// <param name="Variables">Persists the variable changes made by scripts.</param>
public sealed record ScriptSendServices(
    ScriptedRequestSender Sender,
    IScriptChainProvider Chains,
    ConsoleViewModel Console,
    IScriptVariableApplier Variables);
