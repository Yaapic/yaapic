namespace Yaapic.Domain.Scripting;

/// <summary>Where a console entry comes from.</summary>
public enum ConsoleSource
{
    /// <summary>Written by a script.</summary>
    Script,

    /// <summary>Written by the network layer.</summary>
    Network,

    /// <summary>Written by a test.</summary>
    Test,

    /// <summary>Written by Yaapic itself, for example phase headers or the variable-scope warning.</summary>
    System,
}
