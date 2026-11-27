namespace Yaapic.ViewModels.Runner;

/// <summary>Which result rows the runner shows.</summary>
public enum RunnerFilter
{
    /// <summary>Every row.</summary>
    All,

    /// <summary>Rows that passed.</summary>
    Passed,

    /// <summary>Rows that failed a test or ended in error.</summary>
    Failed,

    /// <summary>Rows that were skipped.</summary>
    Skipped,
}
