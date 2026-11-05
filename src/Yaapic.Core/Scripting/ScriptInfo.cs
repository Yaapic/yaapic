namespace Yaapic.Core.Scripting;

/// <summary>The data behind <c>pm.info</c>.</summary>
/// <param name="EventName">Either <c>prerequest</c> or <c>test</c> (Postman names).</param>
/// <param name="RequestName">The name of the request being sent.</param>
/// <param name="RequestId">The id of the request being sent.</param>
/// <param name="Origin">Free text naming the owner of the running script.</param>
public sealed record ScriptInfo(string EventName, string RequestName, Guid RequestId, string Origin);
