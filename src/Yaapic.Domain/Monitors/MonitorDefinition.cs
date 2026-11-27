namespace Yaapic.Domain.Monitors;

/// <summary>A monitor: a folder run on a schedule.</summary>
/// <param name="Id">The stable identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="FolderId">The collection folder to run.</param>
/// <param name="EnvironmentId">The environment to run with, or <see langword="null"/> for none.</param>
/// <param name="IntervalMinutes">The minutes between two runs, one of <see cref="AllowedIntervals"/>.</param>
/// <param name="Enabled">Whether the monitor is scheduled.</param>
public sealed record MonitorDefinition(Guid Id, string Name, Guid FolderId, Guid? EnvironmentId, int IntervalMinutes, bool Enabled)
{
    /// <summary>Gets the intervals a monitor may use, in minutes.</summary>
    public static IReadOnlyList<int> AllowedIntervals { get; } = [5, 10, 15, 30, 60, 180, 360, 720, 1440];

    /// <summary>Gets a value indicating whether the name is not blank and the interval is allowed.</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && AllowedIntervals.Contains(IntervalMinutes);

    /// <summary>Creates an enabled monitor running every 60 minutes without environment.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="folderId">The folder to run.</param>
    /// <returns>The new monitor.</returns>
    public static MonitorDefinition Create(string name, Guid folderId) => new(Guid.NewGuid(), name, folderId, null, 60, true);
}
