namespace Yaapic.Core.LoadTesting;

/// <summary>Pure functions describing how many virtual users are active over time.</summary>
public static class VirtualUserSchedule
{
    /// <summary>Gets the number of virtual users that should be active.</summary>
    /// <param name="plan">The load-test plan.</param>
    /// <param name="elapsed">The time since the test started; negative counts as zero.</param>
    /// <returns>The target user count, 0 once the duration is over.</returns>
    public static int TargetUsers(LoadTestPlan plan, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed >= plan.Duration)
        {
            return 0;
        }

        if (plan.Profile == LoadProfile.Fixed || elapsed >= plan.RampUp)
        {
            return plan.VirtualUsers;
        }

        return Math.Max(1, (int)Math.Ceiling(plan.VirtualUsers * elapsed.TotalSeconds / plan.RampUp.TotalSeconds));
    }

    /// <summary>Gets the elapsed time at which a virtual user starts.</summary>
    /// <param name="plan">The load-test plan.</param>
    /// <param name="userIndex">The zero-based user index.</param>
    /// <returns>The first elapsed time at which <see cref="TargetUsers"/> exceeds <paramref name="userIndex"/>.</returns>
    public static TimeSpan StartOffset(LoadTestPlan plan, int userIndex)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Profile == LoadProfile.Fixed || userIndex <= 0)
        {
            return TimeSpan.Zero;
        }

        TimeSpan offset = plan.RampUp * userIndex / plan.VirtualUsers;

        // Tick rounding can land just before the exact threshold; step forward until both functions agree.
        while (TargetUsers(plan, offset) <= userIndex && offset < plan.Duration)
        {
            offset += TimeSpan.FromTicks(1);
        }

        return offset;
    }

    /// <summary>Lists the distinct hosts the plan calls.</summary>
    /// <param name="plan">The load-test plan.</param>
    /// <param name="resolve">Turns a URL template into a URL.</param>
    /// <returns>The distinct <c>scheme://host:port</c> values, comma separated, in first-seen order; invalid URLs are listed as they are.</returns>
    public static string DescribeHosts(LoadTestPlan plan, Func<string, string> resolve)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(resolve);
        var hosts = new List<string>();
        foreach (var item in plan.Items)
        {
            string url = resolve(item.Request.Url);
            string host = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
                ? $"{uri.Scheme}://{uri.Host}:{uri.Port}"
                : url;
            if (!hosts.Contains(host))
            {
                hosts.Add(host);
            }
        }

        return string.Join(", ", hosts);
    }
}
