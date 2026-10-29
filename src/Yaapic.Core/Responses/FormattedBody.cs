namespace Yaapic.Core.Responses;

/// <summary>A response body decoded for display.</summary>
/// <param name="Text">The text to show; empty for a binary body.</param>
/// <param name="Language">One of <c>json</c>, <c>xml</c>, <c>html</c> or <c>text</c>.</param>
/// <param name="IsBinary">Whether the body is binary and has no text form.</param>
/// <param name="PrettyApplied">Whether <paramref name="Text"/> was re-indented.</param>
public sealed record FormattedBody(string Text, string Language, bool IsBinary, bool PrettyApplied);
