namespace Yaapic.Core.Scripting;

/// <summary>The outcome of a <c>yaapic.sendRequest</c> call.</summary>
/// <param name="Response">The response, or <see langword="null"/> on failure.</param>
/// <param name="Error">The failure text (timeout, DNS, invalid URL, send limit reached), or <see langword="null"/>.</param>
public sealed record ScriptSendResult(ScriptResponse? Response, string? Error);
