using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Responses;
using Yaapic.Core.Runner;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Console;

namespace Yaapic.ViewModels.Runner;

/// <summary>The Runner window: starts and stops a run, shows its progress and results and exports the reports.</summary>
public sealed partial class RunnerViewModel : ObservableObject
{
    private readonly CollectionRunner _runner;
    private readonly ConsoleViewModel _console;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _cts;
    private bool _showIteration;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportJsonCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportJUnitCommand))]
    private RunResult? _result;

    [ObservableProperty]
    private RunnerFilter _filter;

    [ObservableProperty]
    private RunnerResultRowViewModel? _selectedRow;

    [ObservableProperty]
    private string? _exportMessage;

    [ObservableProperty]
    private int _summarySent;

    [ObservableProperty]
    private int _summaryErrors;

    [ObservableProperty]
    private int _summaryFailed;

    [ObservableProperty]
    private int _summarySkipped;

    [ObservableProperty]
    private string _summaryTests = "";

    [ObservableProperty]
    private string _summaryAverage = "";

    [ObservableProperty]
    private string _summaryDuration = "";

    /// <summary>Initializes a new instance of the <see cref="RunnerViewModel"/> class.</summary>
    /// <param name="setup">The setup side of the window.</param>
    /// <param name="runner">The collection runner.</param>
    /// <param name="console">The console that receives the reports of every request.</param>
    /// <param name="dialogs">The dialog service, used to save the reports.</param>
    public RunnerViewModel(RunnerSetupViewModel setup, CollectionRunner runner, ConsoleViewModel console, IDialogService dialogs)
    {
        Setup = setup;
        _runner = runner;
        _console = console;
        _dialogs = dialogs;
        setup.PropertyChanged += OnSetupChanged;
    }

    /// <summary>Occurs when a run starts.</summary>
    public event EventHandler? RunStarted;

    /// <summary>Occurs when a run ended, whatever the status.</summary>
    public event EventHandler<RunResult>? RunCompleted;

    /// <summary>Gets the setup side of the window.</summary>
    public RunnerSetupViewModel Setup { get; }

    /// <summary>Gets every result row of the current run.</summary>
    public ObservableCollection<RunnerResultRowViewModel> Rows { get; } = [];

    /// <summary>Gets the rows that match <see cref="Filter"/>.</summary>
    public ObservableCollection<RunnerResultRowViewModel> VisibleRows { get; } = [];

    /// <summary>Gets the test results of the selected row.</summary>
    public IReadOnlyList<TestResult> SelectedTests => SelectedRow?.Result.Tests ?? [];

    /// <summary>Gets the console lines of the selected row, as "[level] message".</summary>
    public IReadOnlyList<string> SelectedConsole =>
        [.. (SelectedRow?.Result.Console ?? []).Select(static e => $"[{e.Level.ToString().ToLowerInvariant()}] {e.Message}")];

    /// <summary>Gets the error text of the selected row, or <see langword="null"/>.</summary>
    public string? SelectedError => SelectedRow?.Result.Error;

    /// <summary>Loads a folder in the setup, unless a run is going on.</summary>
    /// <param name="folder">The run folder.</param>
    public void Open(CollectionNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!IsRunning)
        {
            Setup.Load(folder);
        }
    }

    partial void OnIsRunningChanged(bool value) => Setup.IsFolderLocked = value;

    partial void OnFilterChanged(RunnerFilter value) => RefreshVisible();

    partial void OnSelectedRowChanged(RunnerResultRowViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedTests));
        OnPropertyChanged(nameof(SelectedConsole));
        OnPropertyChanged(nameof(SelectedError));
    }

    private bool CanRunNow() => Setup.CanRun && !IsRunning;

    private bool CanStop() => IsRunning;

    private bool HasResult() => Result is not null;

    [RelayCommand(CanExecute = nameof(CanRunNow))]
    private async Task RunAsync()
    {
        RunPlan plan = Setup.BuildPlan();
        _showIteration = plan.Options.Iterations > 1;
        ClearResults();
        IsRunning = true;
        using CancellationTokenSource cts = new();
        _cts = cts;
        RunStarted?.Invoke(this, EventArgs.Empty);
        RunResult result;
        try
        {
            result = await _runner.RunAsync(plan, new ContextProgress<RunProgress>(OnProgress), cts.Token);
        }
        finally
        {
            _cts = null;
            IsRunning = false;
        }

        Result = result;
        ShowSummary(result);
        StatusText = result.Status switch
        {
            RunStatus.Completed => Strings.RunnerStatusCompleted,
            RunStatus.Stopped => Strings.RunnerStatusStopped,
            RunStatus.StoppedOnFailure => Strings.RunnerStatusStoppedOnFailure,
            RunStatus.Canceled => Strings.RunnerStatusCanceled,
            _ => Strings.RunnerStatusFailed(result.Error),
        };
        RunCompleted?.Invoke(this, result);
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(HasResult))]
    private Task ExportJsonAsync() => ExportAsync(RunReportJson.Write, RunReportJson.SuggestedFileName);

    [RelayCommand(CanExecute = nameof(HasResult))]
    private Task ExportJUnitAsync() => ExportAsync(RunReportJUnit.Write, RunReportJUnit.SuggestedFileName);

    private async Task ExportAsync(Func<RunResult, byte[]> write, Func<RunResult, string> name)
    {
        if (Result is not { } result)
        {
            return;
        }

        string fileName = name(result);
        bool saved = await _dialogs.SaveFileAsync(fileName, write(result));
        ExportMessage = saved ? Strings.RunnerExported(fileName) : null;
    }

    private void OnProgress(RunProgress p)
    {
        if (p.LastReport is { } report)
        {
            _console.AppendReport(report);
            foreach (string warning in report.Warnings)
            {
                _console.AppendSystem(ConsoleLevel.Warn, warning);
            }
        }

        if (p.Last is { } last)
        {
            var row = new RunnerResultRowViewModel(p.Iteration, last, _showIteration);
            Rows.Add(row);
            if (row.Matches(Filter))
            {
                VisibleRows.Add(row);
            }
        }

        ProgressText = $"{p.Completed}/{p.Total}";
        Progress = p.Total > 0 ? (double)p.Completed / p.Total : 0;
        StatusText = Strings.RunnerRunning(p.Completed, p.Total);
    }

    private void ClearResults()
    {
        Result = null;
        SelectedRow = null;
        Rows.Clear();
        VisibleRows.Clear();
        ExportMessage = null;
        ProgressText = "";
        Progress = 0;
        StatusText = "";
        SummarySent = SummaryErrors = SummaryFailed = SummarySkipped = 0;
        SummaryTests = SummaryAverage = SummaryDuration = "";
    }

    private void ShowSummary(RunResult result)
    {
        RunSummary s = RunSummary.From(result);
        SummarySent = s.Sent;
        SummaryErrors = s.Errors;
        SummaryFailed = s.FailedRequests;
        SummarySkipped = s.Skipped;
        SummaryTests = string.Create(CultureInfo.InvariantCulture, $"{s.TestsPassed}/{s.TestsPassed + s.TestsFailed}");
        SummaryAverage = ResponseText.FormatDuration(s.AverageResponseTime);
        SummaryDuration = ResponseText.FormatDuration(s.TotalDuration);
    }

    private void RefreshVisible()
    {
        VisibleRows.Clear();
        foreach (RunnerResultRowViewModel row in Rows.Where(r => r.Matches(Filter)))
        {
            VisibleRows.Add(row);
        }
    }

    private void OnSetupChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RunnerSetupViewModel.CanRun))
        {
            RunCommand.NotifyCanExecuteChanged();
        }
    }
}
