using Yaapic.Domain.Http;

namespace Yaapic.ViewModels.Requests;

/// <summary>One entry of the HTTP version picker.</summary>
/// <param name="Value">The preference the entry selects.</param>
/// <param name="Label">The localized label.</param>
/// <param name="IsAvailable">Whether the entry can be chosen on this machine.</param>
public sealed record HttpVersionChoice(HttpVersionPreference Value, string Label, bool IsAvailable);
