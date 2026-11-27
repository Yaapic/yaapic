namespace Yaapic.Core.Runner;

/// <summary>How one request of a run ended.</summary>
public enum RunRequestOutcome
{
    /// <summary>The request was sent and no test failed.</summary>
    Passed,

    /// <summary>The request was sent and at least one test failed.</summary>
    Failed,

    /// <summary>The send failed or a script aborted it.</summary>
    Error,

    /// <summary>A pre-request script skipped the request.</summary>
    Skipped,
}
