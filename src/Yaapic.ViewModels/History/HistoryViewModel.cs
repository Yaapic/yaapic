using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Http;
using Yaapic.Core.Storage;
using Yaapic.Domain.History;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.History;

/// <summary>The request history: recording of sends and a filtered, day-grouped list.</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly IHistoryStore _store;
    private readonly TimeProvider _time;
    private readonly TabsViewModel _tabs;
    private readonly IDialogService _dialogs;
    private List<HistoryEntry> _entries = [];

    [ObservableProperty]
    private string _filterText = "";

    [ObservableProperty]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string? _lastError;

    /// <summary>Initializes a new instance of the <see cref="HistoryViewModel"/> class.</summary>
    /// <param name="store">The history store.</param>
    /// <param name="time">The clock and local time zone.</param>
    /// <param name="tabs">The open tabs, where entries are reopened.</param>
    /// <param name="dialogs">Confirms clearing the whole history.</param>
    public HistoryViewModel(IHistoryStore store, TimeProvider time, TabsViewModel tabs, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(tabs);
        ArgumentNullException.ThrowIfNull(dialogs);
        _store = store;
        _time = time;
        _tabs = tabs;
        _dialogs = dialogs;
    }

    /// <summary>Gets the filtered entries grouped by local day, newest day first.</summary>
    public ObservableCollection<HistoryGroup> Groups { get; } = [];

    /// <summary>Records a completed send; canceled sends are skipped and storage errors go to <see cref="LastError"/>.</summary>
    /// <param name="request">The request as sent.</param>
    /// <param name="report">The outcome of the send.</param>
    /// <returns>A task that completes once the entry is stored; it never faults on a storage error.</returns>
    public async Task RecordAsync(HttpRequestDefinition request, SendReport report)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(report);
        if (report.Result.Failure?.Kind == SendFailureKind.Canceled)
        {
            return;
        }

        var entry = HistoryEntry.From(request, report.Result, _time.GetUtcNow());
        try
        {
            await _store.AppendAsync(entry, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        _entries.Insert(0, entry);
        Rebuild();
    }

    /// <summary>Reloads the entries from the store.</summary>
    /// <returns>A task that completes once the list is rebuilt; it never faults on a storage error.</returns>
    public async Task RefreshAsync()
    {
        try
        {
            _entries = [.. await _store.ListAsync(CancellationToken.None)];
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        Rebuild();
    }

    [RelayCommand]
    private void Open(HistoryItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _tabs.OpenAsNew(item.Entry.Request);
    }

    [RelayCommand]
    private async Task DeleteAsync(HistoryItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            await _store.DeleteAsync(item.Id, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        _entries.RemoveAll(e => e.Id == item.Id);
        Rebuild();
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        if (!await _dialogs.ConfirmDeleteAsync(Strings.HistoryDeleteAllTarget))
        {
            return;
        }

        try
        {
            await _store.ClearAsync(CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        _entries = [];
        Rebuild();
    }

    partial void OnFilterTextChanged(string value) => Rebuild();

    private static bool Matches(HistoryItemViewModel item, string filter) =>
        item.MethodText.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || item.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || item.Url.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || item.StatusText.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string Title(DateTime day, DateTime today) => (today - day).Days switch
    {
        0 => Strings.HistoryToday,
        1 => Strings.HistoryYesterday,
        _ => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    private void Rebuild()
    {
        IsEmpty = _entries.Count == 0;
        var filter = FilterText.Trim();
        var zone = _time.LocalTimeZone;
        var today = TimeZoneInfo.ConvertTime(_time.GetUtcNow(), zone).Date;
        var groups = _entries
            .Select(e => new HistoryItemViewModel(e, TimeZoneInfo.ConvertTime(e.At, zone)))
            .Where(i => filter.Length == 0 || Matches(i, filter))
            .GroupBy(i => TimeZoneInfo.ConvertTime(i.Entry.At, zone).Date)
            .OrderByDescending(g => g.Key);

        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(new HistoryGroup(Title(group.Key, today), [.. group]));
        }
    }
}
