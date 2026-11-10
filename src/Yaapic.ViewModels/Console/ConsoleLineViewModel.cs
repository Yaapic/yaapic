using System.Globalization;

namespace Yaapic.ViewModels.Console;

/// <summary>One immutable line of the console.</summary>
public sealed class ConsoleLineViewModel
{
    /// <summary>Initializes a new instance of the <see cref="ConsoleLineViewModel"/> class.</summary>
    /// <param name="at">The time of the line, converted to local time for display.</param>
    /// <param name="kind">The kind of line.</param>
    /// <param name="message">The message.</param>
    /// <param name="origin">The owner of the line, or empty.</param>
    /// <param name="method">The HTTP verb for network lines, or empty.</param>
    /// <param name="isError">Whether the line counts as an error; by default error and failed-test lines do.</param>
    public ConsoleLineViewModel(DateTimeOffset at, ConsoleLineKind kind, string message, string origin = "", string method = "", bool? isError = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(method);
        TimeText = at.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        Kind = kind;
        Message = message;
        Origin = origin;
        Method = method;
        IsError = isError ?? kind is ConsoleLineKind.Error or ConsoleLineKind.TestFailed;
        Symbol = kind switch
        {
            ConsoleLineKind.Header => "›",
            ConsoleLineKind.Info => "i",
            ConsoleLineKind.Warn => "!",
            ConsoleLineKind.Error or ConsoleLineKind.TestFailed => "×",
            ConsoleLineKind.Network => "↗",
            ConsoleLineKind.TestPassed => "✓",
            _ => "·",
        };
    }

    /// <summary>Gets the local time as <c>HH:mm:ss.fff</c>.</summary>
    public string TimeText { get; }

    /// <summary>Gets the symbol of the line kind.</summary>
    public string Symbol { get; }

    /// <summary>Gets the kind of line.</summary>
    public ConsoleLineKind Kind { get; }

    /// <summary>Gets the message.</summary>
    public string Message { get; }

    /// <summary>Gets the owner of the line, or empty.</summary>
    public string Origin { get; }

    /// <summary>Gets the HTTP verb for network lines, or empty.</summary>
    public string Method { get; }

    /// <summary>Gets a value indicating whether the line counts as an error.</summary>
    public bool IsError { get; }

    /// <summary>Gets the line as plain text, as copied to the clipboard.</summary>
    public string Text => string.Join(' ', new[] { TimeText, Symbol, Method, Message, Origin.Length == 0 ? "" : $"[{Origin}]" }.Where(s => s.Length > 0));
}
