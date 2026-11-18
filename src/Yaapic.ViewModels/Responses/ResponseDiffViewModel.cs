using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Diff;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Responses;

/// <summary>Keeps the previous response and an optional pinned baseline of a tab and exposes a ready-to-render diff.</summary>
public sealed partial class ResponseDiffViewModel : ObservableObject
{
    private const int FoldThreshold = 6;
    private const int Context = 3;

    [ObservableProperty]
    private bool _ignoreWhitespace;

    [ObservableProperty]
    private bool _ignoreVolatileHeaders = true;

    [ObservableProperty]
    private bool _expandFolds;

    [ObservableProperty]
    private bool _useBaseline;

    private ResponseSnapshot? _previous;
    private ResponseSnapshot? _current;
    private ResponseSnapshot? _baseline;
    private string? _unavailableReason;
    private string _pinnedText = "";
    private bool _hasComparison;
    private string _summaryText = "";
    private string _statusLine = "";
    private IReadOnlyList<HeaderDiffItem> _headers = [];
    private IReadOnlyList<DiffItem> _bodyLines = [];

    /// <summary>Gets the response before <see cref="Current"/>.</summary>
    public ResponseSnapshot? Previous { get => _previous; private set => SetProperty(ref _previous, value); }

    /// <summary>Gets the latest comparable response.</summary>
    public ResponseSnapshot? Current { get => _current; private set => SetProperty(ref _current, value); }

    /// <summary>Gets the pinned baseline, or <see langword="null"/>.</summary>
    public ResponseSnapshot? Baseline
    {
        get => _baseline;
        private set
        {
            if (SetProperty(ref _baseline, value))
            {
                OnPropertyChanged(nameof(HasBaseline));
                PinnedText = value is null ? "" : Strings.DiffPinnedAt(value.At.ToString("T", CultureInfo.CurrentCulture));
                UnpinCommand.NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets a value indicating whether a baseline is pinned, which enables <see cref="UseBaseline"/>.</summary>
    public bool HasBaseline => _baseline is not null;

    /// <summary>Gets the text telling when the baseline was pinned; empty without baseline.</summary>
    public string PinnedText { get => _pinnedText; private set => SetProperty(ref _pinnedText, value); }

    /// <summary>Gets why no comparison is possible (too large or binary), or <see langword="null"/>.</summary>
    public string? UnavailableReason { get => _unavailableReason; private set => SetProperty(ref _unavailableReason, value); }

    /// <summary>Gets a value indicating whether two snapshots are available to compare.</summary>
    public bool HasComparison { get => _hasComparison; private set => SetProperty(ref _hasComparison, value); }

    /// <summary>Gets the one-line summary of the differences.</summary>
    public string SummaryText { get => _summaryText; private set => SetProperty(ref _summaryText, value); }

    /// <summary>Gets <c>200 OK -&gt; 404 Not Found</c> when the status code changed; otherwise empty.</summary>
    public string StatusLine { get => _statusLine; private set => SetProperty(ref _statusLine, value); }

    /// <summary>Gets the changed headers.</summary>
    public IReadOnlyList<HeaderDiffItem> Headers { get => _headers; private set => SetProperty(ref _headers, value); }

    /// <summary>Gets the body lines to render, with long equal runs folded unless <see cref="ExpandFolds"/>.</summary>
    public IReadOnlyList<DiffItem> BodyLines { get => _bodyLines; private set => SetProperty(ref _bodyLines, value); }

    /// <summary>Records a new successful response.</summary>
    /// <param name="response">The response.</param>
    /// <param name="at">When it was received.</param>
    public void Record(HttpResponseData response, DateTimeOffset at)
    {
        var snapshot = ResponseSnapshot.TryCreate(response, at);
        if (snapshot is null)
        {
            // Binary or too large: the old snapshots stay, so the baseline and the last comparison survive.
            UnavailableReason = Strings.DiffTooLarge;
            return;
        }

        UnavailableReason = null;
        Previous = Current;
        Current = snapshot;
        PinCommand.NotifyCanExecuteChanged();
        Recompute();
    }

    /// <summary>Forgets the previous and current responses but keeps the pinned baseline.</summary>
    public void Clear()
    {
        UnavailableReason = null;
        Previous = null;
        Current = null;
        PinCommand.NotifyCanExecuteChanged();
        Recompute();
    }

    [RelayCommand(CanExecute = nameof(CanPin))]
    private void Pin() => Baseline = Current;

    private bool CanPin() => _current is not null;

    [RelayCommand(CanExecute = nameof(HasBaseline))]
    private void Unpin()
    {
        Baseline = null;
        UseBaseline = false;
        Recompute();
    }

    partial void OnIgnoreWhitespaceChanged(bool value) => Recompute();

    partial void OnIgnoreVolatileHeadersChanged(bool value) => Recompute();

    partial void OnExpandFoldsChanged(bool value) => Recompute();

    partial void OnUseBaselineChanged(bool value)
    {
        if (value && _baseline is null)
        {
            UseBaseline = false;
            return;
        }

        Recompute();
    }

    private void Recompute()
    {
        var old = UseBaseline ? _baseline : _previous;
        if (old is null || _current is null)
        {
            ShowNone();
            return;
        }

        ResponseDiff diff;
        try
        {
            diff = ResponseDiffer.Compare(old, _current, new ResponseDiffOptions(IgnoreWhitespace, IgnoreVolatileHeaders));
        }
        catch (ArgumentException)
        {
            // Over the line guard of LineDiff.
            UnavailableReason = Strings.DiffTooLarge;
            ShowNone();
            return;
        }

        UnavailableReason = null;
        HasComparison = true;
        var changes = diff.BodyStats.Added + diff.BodyStats.Removed + diff.Headers.Count;
        SummaryText = changes == 0 && !diff.StatusChanged
            ? Strings.DiffNoDifferences
            : Strings.DiffSummary(diff.BodyStats.Added, diff.BodyStats.Removed, diff.Headers.Count);
        StatusLine = diff.StatusChanged ? $"{diff.OldStatus} -> {diff.NewStatus}" : "";
        Headers = [.. diff.Headers.Select(h => new HeaderDiffItem(h.Name, h.Kind, h.OldValue ?? "", h.NewValue ?? ""))];
        BodyLines = diff.BodyStats.HasChanges ? Fold(diff.Body, ExpandFolds) : [];
    }

    private void ShowNone()
    {
        HasComparison = false;
        SummaryText = "";
        StatusLine = "";
        Headers = [];
        BodyLines = [];
    }

    private static List<DiffItem> Fold(IReadOnlyList<DiffLine> lines, bool expand)
    {
        var items = new List<DiffItem>(lines.Count);
        var i = 0;
        while (i < lines.Count)
        {
            if (lines[i].Op != DiffOp.Equal)
            {
                items.Add(ToItem(lines[i++]));
                continue;
            }

            var end = i;
            while (end < lines.Count && lines[end].Op == DiffOp.Equal)
            {
                end++;
            }

            var count = end - i;
            if (expand || count <= FoldThreshold)
            {
                for (; i < end; i++)
                {
                    items.Add(ToItem(lines[i]));
                }

                continue;
            }

            var head = i == 0 ? 0 : Context;
            var tail = end == lines.Count ? 0 : Context;
            for (var k = 0; k < head; k++)
            {
                items.Add(ToItem(lines[i + k]));
            }

            items.Add(new DiffItem(DiffOp.Equal, Strings.DiffFolded(count - head - tail), "", "", true));
            for (var k = end - tail; k < end; k++)
            {
                items.Add(ToItem(lines[k]));
            }

            i = end;
        }

        return items;
    }

    private static DiffItem ToItem(DiffLine l) => new(
        l.Op,
        l.Text,
        l.OldLine?.ToString(CultureInfo.InvariantCulture) ?? "",
        l.NewLine?.ToString(CultureInfo.InvariantCulture) ?? "",
        false);
}
