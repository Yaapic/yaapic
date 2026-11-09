using System.Globalization;

namespace Yaapic.Core.Responses;

/// <summary>Formats response durations and sizes for display.</summary>
public static class ResponseText
{
    /// <summary>Formats a duration, such as <c>120 ms</c> or <c>1.23 s</c>.</summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The display text.</returns>
    public static string FormatDuration(TimeSpan duration)
    {
        var ms = duration.TotalMilliseconds;
        return ms < 1000
            ? $"{Math.Round(ms).ToString(CultureInfo.InvariantCulture)} ms"
            : $"{(ms / 1000).ToString("0.00", CultureInfo.InvariantCulture)} s";
    }

    /// <summary>Formats a byte count, such as <c>512 B</c>, <c>1.5 KB</c> or <c>2.0 MB</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>The display text.</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes.ToString(CultureInfo.InvariantCulture)} B",
        < 1024 * 1024 => $"{(bytes / 1024.0).ToString("0.0", CultureInfo.InvariantCulture)} KB",
        _ => $"{(bytes / (1024.0 * 1024)).ToString("0.0", CultureInfo.InvariantCulture)} MB",
    };
}
