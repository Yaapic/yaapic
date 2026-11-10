namespace Yaapic.ViewModels.Console;

/// <summary>The level filter of the console.</summary>
public enum ConsoleFilter
{
    /// <summary>Every line.</summary>
    All,

    /// <summary>Log, info, debug, network and passed-test lines.</summary>
    Log,

    /// <summary>Warnings.</summary>
    Warn,

    /// <summary>Errors, failed tests and failed network exchanges.</summary>
    Error,
}
