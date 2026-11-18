using Yaapic.Domain.Http;

namespace Yaapic.Core.Diff;

/// <summary>How a header differs between two responses.</summary>
public enum HeaderDiffKind
{
    /// <summary>The header exists only in the new response.</summary>
    Added,

    /// <summary>The header exists only in the old response.</summary>
    Removed,

    /// <summary>The header exists in both responses with different values.</summary>
    Changed,
}

/// <summary>One header that differs between two responses.</summary>
/// <param name="Name">The header name, as written in the response that has it (the new one when both do).</param>
/// <param name="Kind">The kind of difference.</param>
/// <param name="OldValue">The old values joined with <c>, </c>, or <see langword="null"/> when added.</param>
/// <param name="NewValue">The new values joined with <c>, </c>, or <see langword="null"/> when removed.</param>
public sealed record HeaderDiff(string Name, HeaderDiffKind Kind, string? OldValue, string? NewValue);

/// <summary>The difference between two response snapshots.</summary>
/// <param name="StatusChanged">Whether the status code changed (the reason phrase is ignored).</param>
/// <param name="OldStatus">The old status, such as <c>200 OK</c>.</param>
/// <param name="NewStatus">The new status, such as <c>404 Not Found</c>.</param>
/// <param name="Headers">The changed headers, sorted by name.</param>
/// <param name="Body">The body line diff.</param>
/// <param name="BodyStats">The body diff counts.</param>
public sealed record ResponseDiff(
    bool StatusChanged,
    string OldStatus,
    string NewStatus,
    IReadOnlyList<HeaderDiff> Headers,
    IReadOnlyList<DiffLine> Body,
    DiffStats BodyStats);

/// <summary>Options of <see cref="ResponseDiffer.Compare"/>.</summary>
/// <param name="IgnoreWhitespace">Compares body lines after trimming.</param>
/// <param name="IgnoreVolatileHeaders">Skips the headers listed in <see cref="ResponseDiffer.VolatileHeaders"/>.</param>
public sealed record ResponseDiffOptions(bool IgnoreWhitespace = false, bool IgnoreVolatileHeaders = true);
