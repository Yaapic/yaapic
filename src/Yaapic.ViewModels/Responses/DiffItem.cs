using Yaapic.Core.Diff;

namespace Yaapic.ViewModels.Responses;

/// <summary>One display line of the body diff: a changed or context line, or a fold standing for hidden equal lines.</summary>
/// <param name="Op">What happened to the line.</param>
/// <param name="Text">The line text, or the fold label when <paramref name="IsFold"/> is <see langword="true"/>.</param>
/// <param name="OldNumber">The 1-based line number on the old side; empty when absent.</param>
/// <param name="NewNumber">The 1-based line number on the new side; empty when absent.</param>
/// <param name="IsFold">Whether the item stands for collapsed equal lines.</param>
public sealed record DiffItem(DiffOp Op, string Text, string OldNumber, string NewNumber, bool IsFold);

/// <summary>One changed header of the diff, with display-ready values.</summary>
/// <param name="Name">The header name.</param>
/// <param name="Kind">Whether the header was added, removed or changed.</param>
/// <param name="OldValue">The old value; empty when added.</param>
/// <param name="NewValue">The new value; empty when removed.</param>
public sealed record HeaderDiffItem(string Name, HeaderDiffKind Kind, string OldValue, string NewValue);
