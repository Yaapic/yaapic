namespace Yaapic.Domain.Scripting;

/// <summary>How a script run ended.</summary>
public enum ScriptRunStatus
{
    /// <summary>The script ran to the end.</summary>
    Succeeded,

    /// <summary>The script threw or failed to compile.</summary>
    Failed,

    /// <summary>The script exceeded its time limit.</summary>
    TimedOut,

    /// <summary>The script exceeded its memory limit.</summary>
    MemoryLimit,

    /// <summary>The run was canceled.</summary>
    Canceled,

    /// <summary>The engine for the language could not be loaded.</summary>
    Unavailable,
}
