namespace Yaapic.Core.Import;

/// <summary>How sure an importer is that it can read an input.</summary>
public enum ImportConfidence
{
    /// <summary>The input is not in this format.</summary>
    None,

    /// <summary>The input may be in this format.</summary>
    Possible,

    /// <summary>The input is in this format.</summary>
    Certain,
}
