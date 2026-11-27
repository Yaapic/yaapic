using System.Globalization;
using Yaapic.Core.Mocks;
using Yaapic.Core.Responses;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Mocks;

/// <summary>One request of the mock server log.</summary>
/// <param name="Entry">The log entry shown.</param>
public sealed record MockLogRowViewModel(MockLogEntry Entry)
{
    /// <summary>Gets the local time of the request, as <c>HH:mm:ss</c>.</summary>
    public string Time => Entry.At.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Gets the request method.</summary>
    public string Method => Entry.Method;

    /// <summary>Gets the request path and query.</summary>
    public string Target => Entry.Target;

    /// <summary>Gets the response status.</summary>
    public int Status => Entry.Status;

    /// <summary>Gets the matched route name, or the "no match" text.</summary>
    public string Route => Entry.RouteName ?? Strings.MockNoMatch;

    /// <summary>Gets the formatted duration.</summary>
    public string DurationText => ResponseText.FormatDuration(Entry.Duration);
}
