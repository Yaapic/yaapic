namespace Yaapic.Domain.Monitors;

/// <summary>How a monitor run ended.</summary>
public enum MonitorRunStatus
{
    /// <summary>Every request passed.</summary>
    Passed,

    /// <summary>At least one request failed a test or errored.</summary>
    Failed,

    /// <summary>The run itself could not complete.</summary>
    Error,
}
