namespace Yaapic.Domain.Scripting;

/// <summary>The severity of a console entry.</summary>
public enum ConsoleLevel
{
    /// <summary>A plain log line.</summary>
    Log,

    /// <summary>An informational line.</summary>
    Info,

    /// <summary>A warning.</summary>
    Warn,

    /// <summary>An error.</summary>
    Error,

    /// <summary>A debug line.</summary>
    Debug,
}
