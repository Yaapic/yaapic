using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Search;

namespace Yaapic.ViewModels.Palette;

/// <summary>The command palette engine: collects items from sources, filters and ranks them, tracks selection and executes.</summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private const int MaxResults = 50;
    private const int MaxRecentShown = 5;

    private readonly IReadOnlyList<IPaletteSource> _sources;
    private readonly List<string> _recent = [];
    private List<PaletteItem> _items = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _query = string.Empty;

    [ObservableProperty]
    private int _selectedIndex;

    [ObservableProperty]
    private string? _lastError;

    /// <summary>Creates the palette.</summary>
    /// <param name="sources">The item sources, in display order.</param>
    public CommandPaletteViewModel(IReadOnlyList<IPaletteSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources;
        Results.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Gets the ranked results for the current query (at most 50).</summary>
    public ObservableCollection<PaletteResult> Results { get; } = [];

    /// <summary>Gets a value indicating whether the palette is open with a non-blank query that matches nothing.</summary>
    public bool IsEmpty => IsOpen && !string.IsNullOrWhiteSpace(Query) && Results.Count == 0;

    partial void OnQueryChanged(string value) => Refresh();

    [RelayCommand]
    private void Open()
    {
        LastError = null;
        var items = new List<PaletteItem>();
        foreach (var source in _sources)
        {
            try
            {
                items.AddRange(source.GetItems());
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        _items = items;
        Query = string.Empty;
        Refresh();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void MoveNext()
    {
        if (Results.Count > 0)
        {
            SelectedIndex = (SelectedIndex + 1) % Results.Count;
        }
    }

    [RelayCommand]
    private void MovePrevious()
    {
        if (Results.Count > 0)
        {
            SelectedIndex = (SelectedIndex - 1 + Results.Count) % Results.Count;
        }
    }

    [RelayCommand]
    private Task ExecuteSelectedAsync() =>
        SelectedIndex >= 0 && SelectedIndex < Results.Count ? ExecuteAsync(Results[SelectedIndex]) : Task.CompletedTask;

    /// <summary>Closes the palette, remembers the item as most recent and runs it.</summary>
    /// <param name="result">The result to execute.</param>
    /// <returns>A task completing when the action has finished; its failures are reported in <see cref="LastError"/>.</returns>
    [RelayCommand]
    private async Task ExecuteAsync(PaletteResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        IsOpen = false;
        _recent.Remove(result.Item.Id);
        _recent.Insert(0, result.Item.Id);
        try
        {
            await result.Item.Execute();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    private int RecencyRank(PaletteItem item)
    {
        var i = _recent.IndexOf(item.Id);
        return i < 0 ? int.MaxValue : i;
    }

    private void Refresh()
    {
        var results = string.IsNullOrWhiteSpace(Query) ? Blank() : Matching(Query);
        Results.Clear();
        foreach (var r in results.Take(MaxResults))
        {
            Results.Add(r);
        }

        SelectedIndex = 0;
    }

    private IEnumerable<PaletteResult> Blank()
    {
        var recent = _recent
            .Select(id => _items.Find(i => i.Id == id))
            .OfType<PaletteItem>()
            .Take(MaxRecentShown)
            .ToList();
        return recent.Concat(_items.Except(recent)).Select(i => new PaletteResult(i, []));
    }

    private IEnumerable<PaletteResult> Matching(string query) =>
        _items
            .Select((item, order) => (item, order, match: FuzzyMatcher.Match(query, item.Title)))
            .Where(x => x.match is not null)
            .OrderBy(x => x, Comparer<(PaletteItem item, int order, FuzzyMatch? match)>.Create((a, b) =>
            {
                var c = FuzzyMatcher.Compare(a.match!, a.item.Title, b.match!, b.item.Title);
                if (c != 0)
                {
                    return c;
                }

                c = RecencyRank(a.item).CompareTo(RecencyRank(b.item));
                return c != 0 ? c : a.order.CompareTo(b.order);
            }))
            .Select(x => new PaletteResult(x.item, x.match!.Indices));
}
