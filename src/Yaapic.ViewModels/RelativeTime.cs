using Yaapic.Localization;

namespace Yaapic.ViewModels;

/// <summary>Formats a time distance as short localized text such as <c>in 2 h</c> or <c>3 days ago</c>.</summary>
public static class RelativeTime
{
    /// <summary>Formats a distance from now using the largest whole unit (seconds, minutes, hours, days).</summary>
    /// <param name="delta">The target time minus now: positive is in the future, negative in the past.</param>
    /// <returns>The localized text; <c>now</c> when the distance is under one second.</returns>
    public static string Format(TimeSpan delta)
    {
        var abs = delta.Duration();
        if (abs < TimeSpan.FromSeconds(1))
        {
            return Strings.RelativeNow;
        }

        var amount = abs.TotalDays >= 2 ? Strings.RelativeDays((long)abs.TotalDays)
            : abs.TotalDays >= 1 ? Strings.RelativeDay(1)
            : abs.TotalHours >= 1 ? Strings.RelativeHours((long)abs.TotalHours)
            : abs.TotalMinutes >= 1 ? Strings.RelativeMinutes((long)abs.TotalMinutes)
            : Strings.RelativeSeconds((long)abs.TotalSeconds);
        return delta > TimeSpan.Zero ? Strings.RelativeInFuture(amount) : Strings.RelativeInPast(amount);
    }
}
