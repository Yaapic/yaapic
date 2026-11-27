namespace Yaapic.Core.Runner;

/// <summary>How a run ended.</summary>
public enum RunStatus
{
    /// <summary>Every iteration ran.</summary>
    Completed,

    /// <summary>A script ended the run.</summary>
    Stopped,

    /// <summary>The run stopped at the first failure (<see cref="RunOptions.StopOnFailure"/>).</summary>
    StoppedOnFailure,

    /// <summary>The user canceled the run.</summary>
    Canceled,

    /// <summary>The run could not continue because of an internal error.</summary>
    Failed,
}
