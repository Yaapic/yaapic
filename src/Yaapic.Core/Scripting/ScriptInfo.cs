namespace Yaapic.Core.Scripting;

/// <summary>The data behind <c>yaapic.info</c>.</summary>
/// <param name="EventName">Either <c>prerequest</c> or <c>test</c> (Postman names).</param>
/// <param name="RequestName">The name of the request being sent.</param>
/// <param name="RequestId">The id of the request being sent.</param>
/// <param name="Origin">Free text naming the owner of the running script.</param>
/// <param name="Iteration">The zero-based iteration index of the run; 0 outside the Runner.</param>
/// <param name="IterationCount">The number of iterations of the run; 1 outside the Runner.</param>
public sealed record ScriptInfo(string EventName, string RequestName, Guid RequestId, string Origin,
    int Iteration = 0,
    int IterationCount = 1);
