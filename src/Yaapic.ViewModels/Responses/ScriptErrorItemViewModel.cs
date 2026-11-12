using System.Globalization;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Responses;

/// <summary>Display-ready form of a script run that did not succeed.</summary>
public sealed class ScriptErrorItemViewModel
{
    /// <summary>Initializes a new instance of the <see cref="ScriptErrorItemViewModel"/> class.</summary>
    /// <param name="run">The run that did not succeed.</param>
    public ScriptErrorItemViewModel(ScriptRunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);
        Origin = run.Origin;
        StatusText = StatusTextFor(run.Status);
        Message = run.Error?.Message ?? "";
        Location = run.Error?.Line is { } line ? Strings.ScriptLine(line.ToString(CultureInfo.InvariantCulture)) : "";
    }

    /// <summary>Gets the owner of the script.</summary>
    public string Origin { get; }

    /// <summary>Gets the status, such as <c>Script error</c> or <c>Timed out</c>.</summary>
    public string StatusText { get; }

    /// <summary>Gets the error message; empty when the run gave none.</summary>
    public string Message { get; }

    /// <summary>Gets the location, such as <c>line 3</c>, or empty.</summary>
    public string Location { get; }

    internal static string StatusTextFor(ScriptRunStatus status) => status switch
    {
        ScriptRunStatus.TimedOut => Strings.ScriptStatusTimedOut,
        ScriptRunStatus.MemoryLimit => Strings.ScriptStatusMemoryLimit,
        ScriptRunStatus.Canceled => Strings.ScriptStatusCanceled,
        ScriptRunStatus.Unavailable => Strings.ScriptStatusUnavailable,
        _ => Strings.ScriptStatusError,
    };
}
