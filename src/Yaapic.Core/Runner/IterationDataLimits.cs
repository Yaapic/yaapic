namespace Yaapic.Core.Runner;

/// <summary>Size limits of an iteration data file.</summary>
public static class IterationDataLimits
{
    /// <summary>The largest data file, in bytes.</summary>
    public const int MaxBytes = 10 * 1024 * 1024;

    /// <summary>The most data rows a file may hold.</summary>
    public const int MaxRows = 100_000;

    /// <summary>The most columns a file may hold.</summary>
    public const int MaxColumns = 1_000;
}
