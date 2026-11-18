namespace Yaapic.Core.Import;

/// <summary>How serious an <see cref="ImportEntry"/> of the import report is.</summary>
public enum ImportSeverity
{
    /// <summary>Informational: something was converted with a noteworthy detail.</summary>
    Info,

    /// <summary>Something was imported with a loss or an approximation.</summary>
    Warning,

    /// <summary>Something could not be imported and was left out.</summary>
    Skipped,
}
