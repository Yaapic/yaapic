namespace Yaapic.Core.LoadTesting;

/// <summary>How the number of virtual users evolves during a load test.</summary>
public enum LoadProfile
{
    /// <summary>All virtual users start at once.</summary>
    Fixed,

    /// <summary>Virtual users start progressively over the ramp-up time.</summary>
    RampUp,
}
