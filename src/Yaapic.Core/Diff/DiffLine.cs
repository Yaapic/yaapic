namespace Yaapic.Core.Diff;

/// <summary>The kind of a line in a diff.</summary>
public enum DiffOp
{
    /// <summary>The line is present on both sides.</summary>
    Equal,

    /// <summary>The line exists only on the new side.</summary>
    Added,

    /// <summary>The line exists only on the old side.</summary>
    Removed,
}

/// <summary>One line of a line diff.</summary>
/// <param name="Op">The kind of line.</param>
/// <param name="Text">The line text, without its terminator.</param>
/// <param name="OldLine">The 1-based line number on the old side, or <see langword="null"/> for an added line.</param>
/// <param name="NewLine">The 1-based line number on the new side, or <see langword="null"/> for a removed line.</param>
public sealed record DiffLine(DiffOp Op, string Text, int? OldLine, int? NewLine);

/// <summary>Counts of a line diff.</summary>
/// <param name="Added">The number of added lines.</param>
/// <param name="Removed">The number of removed lines.</param>
/// <param name="HasChanges">Whether at least one line was added or removed.</param>
public sealed record DiffStats(int Added, int Removed, bool HasChanges);
