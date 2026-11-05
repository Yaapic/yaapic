using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Scripting;

/// <summary>Everything an engine needs for one run.</summary>
/// <param name="Block">The script to run.</param>
/// <param name="Phase">The phase it runs in.</param>
/// <param name="Origin">Free text naming the owner of the script.</param>
/// <param name="Host">The application side of the <c>pm</c> API.</param>
/// <param name="Limits">The limits to enforce.</param>
public sealed record ScriptInvocation(ScriptBlock Block, ScriptPhase Phase, string Origin, IScriptHost Host, ScriptLimits Limits);
