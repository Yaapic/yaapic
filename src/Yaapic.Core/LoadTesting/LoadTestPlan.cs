using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;

namespace Yaapic.Core.LoadTesting;

/// <summary>What a load test executes: the requests, the virtual users and the profile.</summary>
/// <param name="Name">The test name, usually the folder name.</param>
/// <param name="Items">The requests each virtual user loops over, in order.</param>
/// <param name="VirtualUsers">The peak number of concurrent virtual users.</param>
/// <param name="Duration">The test duration.</param>
/// <param name="Profile">The load profile.</param>
/// <param name="RampUp">The ramp-up time; only used by <see cref="LoadProfile.RampUp"/>.</param>
/// <param name="ThinkTimeMs">The pause between two requests of a virtual user, in milliseconds.</param>
/// <param name="Data">The iteration data rows; empty without data.</param>
/// <param name="Limits">The script limits, or <see langword="null"/> for the defaults.</param>
public sealed record LoadTestPlan(
    string Name,
    IReadOnlyList<RunItem> Items,
    int VirtualUsers,
    TimeSpan Duration,
    LoadProfile Profile,
    TimeSpan RampUp,
    int ThinkTimeMs,
    IReadOnlyList<IterationDataRow> Data,
    ScriptLimits? Limits = null)
{
    /// <summary>The largest allowed number of virtual users.</summary>
    public const int MaxVirtualUsers = 100;

    /// <summary>Checks the plan bounds.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A value is out of its allowed range.</exception>
    public void Validate()
    {
        if (Items.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Items), "A load test needs at least one request.");
        }

        if (VirtualUsers is < 1 or > MaxVirtualUsers)
        {
            throw new ArgumentOutOfRangeException(nameof(VirtualUsers), VirtualUsers, $"Virtual users must be 1 to {MaxVirtualUsers}.");
        }

        if (Duration < TimeSpan.FromSeconds(10) || Duration > TimeSpan.FromSeconds(3600))
        {
            throw new ArgumentOutOfRangeException(nameof(Duration), Duration, "Duration must be 10 to 3600 seconds.");
        }

        if (Profile == LoadProfile.RampUp && (RampUp < TimeSpan.FromSeconds(1) || RampUp > Duration))
        {
            throw new ArgumentOutOfRangeException(nameof(RampUp), RampUp, "Ramp-up must be 1 second to the duration.");
        }

        if (ThinkTimeMs is < 0 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(ThinkTimeMs), ThinkTimeMs, "Think time must be 0 to 10000 ms.");
        }
    }
}
