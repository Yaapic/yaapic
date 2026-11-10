namespace Yaapic.ViewModels.Console;

/// <summary>The kind of a console line.</summary>
public enum ConsoleLineKind
{
    /// <summary>A group header introducing one script run.</summary>
    Header,

    /// <summary>A log or debug message.</summary>
    Log,

    /// <summary>An informational message.</summary>
    Info,

    /// <summary>A warning.</summary>
    Warn,

    /// <summary>An error.</summary>
    Error,

    /// <summary>A network exchange.</summary>
    Network,

    /// <summary>A passed test.</summary>
    TestPassed,

    /// <summary>A failed test.</summary>
    TestFailed,
}
