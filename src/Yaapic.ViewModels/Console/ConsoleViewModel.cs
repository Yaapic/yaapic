using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Console;

/// <summary>The application-wide console: a bounded, filterable list of script output, network exchanges and tests. UI-thread only.</summary>
public sealed partial class ConsoleViewModel : ObservableObject
{
    private readonly IDialogService? _dialogs;
    private readonly int _capacity;
    private readonly List<ConsoleLineViewModel> _all = [];
    private readonly ObservableCollection<ConsoleLineViewModel> _lines = [];

    [ObservableProperty]
    private ConsoleFilter _filter = ConsoleFilter.All;

    [ObservableProperty]
    private string _search = "";

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _warnCount;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private bool _isEmpty = true;

    /// <summary>Initializes a new instance of the <see cref="ConsoleViewModel"/> class.</summary>
    /// <param name="dialogs">The dialog service giving clipboard access; without it <see cref="CopyCommand"/> does nothing.</param>
    /// <param name="capacity">The maximum number of retained lines; the oldest are dropped first.</param>
    public ConsoleViewModel(IDialogService? dialogs = null, int capacity = 1000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _dialogs = dialogs;
        _capacity = capacity;
        Lines = new ReadOnlyObservableCollection<ConsoleLineViewModel>(_lines);
    }

    /// <summary>Gets the lines matching the filter and the search.</summary>
    public ReadOnlyObservableCollection<ConsoleLineViewModel> Lines { get; }

    /// <summary>Adds the lines of one script run: header, console entries, tests, test summary and error.</summary>
    /// <param name="run">The run to show.</param>
    public void AppendRun(ScriptRunResult run)
    {
        ArgumentNullException.ThrowIfNull(run);
        AddRun(run);
        Rebuild();
    }

    /// <summary>Adds network lines.</summary>
    /// <param name="entries">The network log entries, in the order given.</param>
    public void AppendNetwork(IEnumerable<ConsoleEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        AddNetwork(entries);
        Rebuild();
    }

    /// <summary>Adds a whole scripted send: pre-request runs, then the network lines by time, then post-response runs.</summary>
    /// <param name="report">The report of the send.</param>
    public void AppendReport(ScriptedSendReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        foreach (var run in report.Runs.Where(r => r.Phase == ScriptPhase.PreRequest))
        {
            AddRun(run);
        }

        AddNetwork(report.NetworkLog.OrderBy(e => e.At)); // OrderBy is stable for equal timestamps.
        foreach (var run in report.Runs.Where(r => r.Phase == ScriptPhase.PostResponse))
        {
            AddRun(run);
        }

        Rebuild();
    }

    /// <summary>Adds one system line, such as a warning raised outside a script run.</summary>
    /// <param name="level">The severity of the line.</param>
    /// <param name="message">The message.</param>
    public void AppendSystem(ConsoleLevel level, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        Add(new(DateTimeOffset.Now, KindOf(level), message, Strings.ConsoleOriginSystem));
        Rebuild();
    }

    [RelayCommand]
    private void Clear()
    {
        _all.Clear();
        Rebuild();
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        if (_dialogs is not null)
        {
            await _dialogs.SetClipboardTextAsync(string.Join('\n', _lines.Select(l => l.Text)));
        }
    }

    partial void OnFilterChanged(ConsoleFilter value) => Rebuild();

    partial void OnSearchChanged(string value) => Rebuild();

    private static ConsoleLineKind KindOf(ConsoleLevel level) => level switch
    {
        ConsoleLevel.Info => ConsoleLineKind.Info,
        ConsoleLevel.Warn => ConsoleLineKind.Warn,
        ConsoleLevel.Error => ConsoleLineKind.Error,
        _ => ConsoleLineKind.Log,
    };

    private void AddRun(ScriptRunResult run)
    {
        var entries = run.Console.OrderBy(e => e.At).ToList();
        var at = entries.Count > 0 ? entries[0].At : DateTimeOffset.Now;
        var origin = run.Origin;
        if (entries.Count > 0 || run.Tests.Count > 0 || !run.Succeeded)
        {
            var phase = run.Phase == ScriptPhase.PreRequest ? Strings.ConsolePhasePreRequest : Strings.ConsolePhasePostResponse;
            Add(new(at, ConsoleLineKind.Header, $"{phase} · {origin}", origin));
        }

        foreach (var entry in entries)
        {
            if (entry.Source == ConsoleSource.Network)
            {
                AddNetwork([entry]);
            }
            else
            {
                Add(new(entry.At, KindOf(entry.Level), entry.Message, entry.Origin ?? origin));
            }
        }

        foreach (var test in run.Tests)
        {
            Add(test.Passed
                ? new(at, ConsoleLineKind.TestPassed, test.Name, origin)
                : new(at, ConsoleLineKind.TestFailed, $"{test.Name} — {test.Message}", origin));
        }

        if (run.Tests.Count > 0)
        {
            var (passed, failed) = TestSummary.Count(run.Tests);
            Add(new(at, ConsoleLineKind.Info, Strings.ConsoleTestsSummary(passed, failed), origin));
        }

        if (!run.Succeeded)
        {
            var message = ScriptErrorItemViewModel.StatusTextFor(run.Status);
            if (run.Error is { } error)
            {
                message += $": {error.Message}";
                if (error.Line is { } line)
                {
                    message += $" ({Strings.ScriptLine(line.ToString(CultureInfo.InvariantCulture))})";
                }
            }

            Add(new(at, ConsoleLineKind.Error, message, origin));
        }
    }

    private void AddNetwork(IEnumerable<ConsoleEntry> entries)
    {
        foreach (var entry in entries)
        {
            var space = entry.Message.IndexOf(' ', StringComparison.Ordinal);
            var (method, message) = space > 0 ? (entry.Message[..space], entry.Message[(space + 1)..]) : ("", entry.Message);
            Add(new(entry.At, ConsoleLineKind.Network, message, entry.Origin ?? "", method, entry.Level == ConsoleLevel.Error));
        }
    }

    private void Add(ConsoleLineViewModel line)
    {
        _all.Add(line);
        if (_all.Count > _capacity)
        {
            _all.RemoveAt(0);
        }
    }

    private bool Matches(ConsoleLineViewModel line)
    {
        var levelOk = Filter switch
        {
            ConsoleFilter.Log => !line.IsError && line.Kind is ConsoleLineKind.Log or ConsoleLineKind.Info or ConsoleLineKind.Network or ConsoleLineKind.TestPassed,
            ConsoleFilter.Warn => line.Kind == ConsoleLineKind.Warn,
            ConsoleFilter.Error => line.IsError,
            _ => true,
        };
        return levelOk && (Search.Length == 0
            || line.Message.Contains(Search, StringComparison.OrdinalIgnoreCase)
            || line.Origin.Contains(Search, StringComparison.OrdinalIgnoreCase));
    }

    private void Rebuild()
    {
        _lines.Clear();
        foreach (var line in _all.Where(Matches))
        {
            _lines.Add(line);
        }

        TotalCount = _all.Count;
        WarnCount = _all.Count(l => l.Kind == ConsoleLineKind.Warn);
        ErrorCount = _all.Count(l => l.IsError);
        IsEmpty = _all.Count == 0;
    }
}
