using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Responses;
using Yaapic.Core.Runner;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Runner;

namespace Yaapic.ViewModels.LoadTesting;

/// <summary>The Load test window: configuration, confirmation, live metrics and charts, results and export.</summary>
public sealed partial class LoadTestViewModel : ObservableObject
{
    // The metrics keep at most this many durations for the percentiles (LoadMetrics default).
    private const int StoredSamples = 2_000_000;

    private readonly LoadTestRunner _runner;
    private readonly IVariableScopeProvider _scopes;
    private readonly IDialogService _dialogs;
    private CancellationTokenSource? _cts;
    private bool _confirmed;
    private int _runSeconds = 1;

    [ObservableProperty]
    private int _virtualUsers = 10;

    [ObservableProperty]
    private int _durationSeconds = 60;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRampUp))]
    private LoadProfile _profile = LoadProfile.Fixed;

    [ObservableProperty]
    private int _rampUpSeconds = 10;

    [ObservableProperty]
    private int _thinkTimeMs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private string _elapsedText = "";

    [ObservableProperty]
    private int _activeUsers;

    [ObservableProperty]
    private long _totalRequests;

    [ObservableProperty]
    private string _throughput = "";

    [ObservableProperty]
    private string _averageText = "";

    [ObservableProperty]
    private string _p95Text = "";

    [ObservableProperty]
    private string _errorRateText = "";

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _throughputPoints = [];

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _responseTimePoints = [];

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _errorPoints = [];

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _userPoints = [];

    [ObservableProperty]
    private string _throughputMax = "";

    [ObservableProperty]
    private string _responseTimeMax = "";

    [ObservableProperty]
    private IReadOnlyList<LoadRequestRow> _requestRows = [];

    [ObservableProperty]
    private IReadOnlyList<string> _errorMessages = [];

    [ObservableProperty]
    private string _note = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private LoadTestResult? _result;

    [ObservableProperty]
    private string? _exportMessage;

    /// <summary>Initializes a new instance of the <see cref="LoadTestViewModel"/> class.</summary>
    /// <param name="requests">The request list and data file of the window.</param>
    /// <param name="runner">The load-test runner.</param>
    /// <param name="scopes">The variable scopes, used to show the hosts in the confirmation.</param>
    /// <param name="dialogs">The dialog service, used to confirm and to save the report.</param>
    public LoadTestViewModel(RunnerSetupViewModel requests, LoadTestRunner runner, IVariableScopeProvider scopes, IDialogService dialogs)
    {
        Requests = requests;
        _runner = runner;
        _scopes = scopes;
        _dialogs = dialogs;
        requests.PropertyChanged += OnRequestsChanged;
    }

    /// <summary>Gets the request list and data file of the window.</summary>
    public RunnerSetupViewModel Requests { get; }

    /// <summary>Gets a value indicating whether the ramp-up field applies.</summary>
    public bool IsRampUp => Profile == LoadProfile.RampUp;

    /// <summary>Gets the first validation error, or <see langword="null"/> when the configuration is valid.</summary>
    public string? ValidationError =>
        VirtualUsers is < 1 or > LoadTestPlan.MaxVirtualUsers ? Strings.LoadUsersRange(LoadTestPlan.MaxVirtualUsers)
        : DurationSeconds is < 10 or > 3600 ? Strings.LoadDurationRange
        : IsRampUp && (RampUpSeconds < 1 || RampUpSeconds > DurationSeconds) ? Strings.LoadRampRange
        : ThinkTimeMs is < 0 or > 10000 ? Strings.LoadThinkRange
        : Requests.SelectedCount == 0 ? Strings.RunnerNothingSelected
        : null;

    /// <summary>Loads a folder in the request list, unless a test is running.</summary>
    /// <param name="folder">The folder to test.</param>
    public void Open(CollectionNode folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        if (!IsRunning)
        {
            Requests.Load(folder);
        }
    }

    partial void OnVirtualUsersChanged(int value) => Revalidate();

    partial void OnDurationSecondsChanged(int value) => Revalidate();

    partial void OnProfileChanged(LoadProfile value) => Revalidate();

    partial void OnRampUpSecondsChanged(int value) => Revalidate();

    partial void OnThinkTimeMsChanged(int value) => Revalidate();

    private bool CanStart() => ValidationError is null && Requests.CanRun && !IsRunning;

    private bool CanStop() => IsRunning;

    private bool HasResult() => Result is not null;

    private void Revalidate()
    {
        OnPropertyChanged(nameof(ValidationError));
        StartCommand.NotifyCanExecuteChanged();
    }

    private void OnRequestsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RunnerSetupViewModel.CanRun) or nameof(RunnerSetupViewModel.ValidationError))
        {
            Revalidate();
        }
    }

    private LoadTestPlan BuildPlan()
    {
        RunPlan setup = Requests.BuildPlan();
        return new LoadTestPlan(
            setup.Name,
            setup.Items,
            VirtualUsers,
            TimeSpan.FromSeconds(DurationSeconds),
            Profile,
            TimeSpan.FromSeconds(RampUpSeconds),
            ThinkTimeMs,
            setup.Data);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        LoadTestPlan plan = BuildPlan();
        if (!_confirmed)
        {
            Guid first = plan.Items[0].Request.Id;
            string hosts = VirtualUserSchedule.DescribeHosts(plan, url => VariableResolver.Resolve(url, _scopes.GetScopes(first).CreateSource()).Text);
            if (!await _dialogs.ConfirmAsync(Strings.LoadConfirmTitle, Strings.LoadConfirm(hosts)))
            {
                return;
            }

            _confirmed = true;
        }

        ClearResults();
        _runSeconds = Math.Max(1, DurationSeconds);
        IsRunning = true;
        using CancellationTokenSource cts = new();
        _cts = cts;
        LoadTestResult result;
        try
        {
            result = await _runner.RunAsync(plan, new ContextProgress<LoadSnapshot>(ShowSnapshot), cts.Token);
        }
        finally
        {
            _cts = null;
            IsRunning = false;
        }

        ShowResult(result);
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _cts?.Cancel();

    [RelayCommand(CanExecute = nameof(HasResult))]
    private async Task ExportAsync()
    {
        if (Result is not { } result)
        {
            return;
        }

        string fileName = LoadTestReportJson.SuggestedFileName(result);
        bool saved = await _dialogs.SaveFileAsync(fileName, LoadTestReportJson.Write(result));
        ExportMessage = saved ? Strings.RunnerExported(fileName) : null;
    }

    private void ShowSnapshot(LoadSnapshot s)
    {
        ElapsedText = $"{Clock(s.Elapsed)} / {Clock(TimeSpan.FromSeconds(_runSeconds))}";
        ActiveUsers = s.ActiveUsers;
        ShowStats(s.Overall);
        ShowCharts(s.Buckets);
    }

    private void ShowResult(LoadTestResult r)
    {
        ElapsedText = $"{Clock(r.Duration)} / {Clock(r.Plan.Duration)}";
        ActiveUsers = 0;
        ShowStats(r.Overall);
        ShowCharts(r.Buckets);
        RequestRows =
        [
            .. r.Plan.Items.Select((item, i) =>
            {
                LoadStats s = r.PerRequest[i];
                return new LoadRequestRow(
                    item.Request.Name,
                    item.Request.Method.ToText(),
                    s.Count,
                    s.Errors,
                    Ms(s.AverageMs),
                    Ms(s.P90Ms),
                    Ms(s.P95Ms),
                    Ms(s.P99Ms),
                    Ms(s.MaxMs));
            }),
        ];
        ErrorMessages = r.ErrorMessages;
        List<string> notes = [];
        if (r.SamplesTruncated)
        {
            notes.Add(Strings.LoadTruncated(StoredSamples));
        }

        if (r.Plan.Items.Any(static i => !i.Request.Scripts.IsEmpty))
        {
            notes.Add(Strings.LoadScriptsSkipped);
        }

        Note = string.Join(' ', notes);
        Result = r;
    }

    private void ShowStats(LoadStats s)
    {
        TotalRequests = s.Count;
        Throughput = string.Create(CultureInfo.InvariantCulture, $"{s.Throughput:F1}/s");
        AverageText = Ms(s.AverageMs);
        P95Text = Ms(s.P95Ms);
        ErrorRateText = string.Create(CultureInfo.InvariantCulture, $"{s.ErrorRate * 100:F1} %");
    }

    private void ShowCharts(IReadOnlyList<LoadBucket> buckets)
    {
        ThroughputPoints = Series(buckets, static b => b.Requests, out double requestsMax);
        ResponseTimePoints = Series(buckets, static b => b.AverageMs, out double timeMax);
        ErrorPoints = Series(buckets, static b => b.Errors, out _);
        UserPoints = Series(buckets, static b => b.ActiveUsers, out _);
        ThroughputMax = string.Create(CultureInfo.InvariantCulture, $"{requestsMax:0.#}/s");
        ResponseTimeMax = Ms(timeMax);
    }

    private ChartPoint[] Series(IReadOnlyList<LoadBucket> buckets, Func<LoadBucket, double> value, out double max)
    {
        max = buckets.Count == 0 ? 0 : buckets.Max(value);
        double scale = Math.Max(max, 1);
        return [.. buckets.Select(b => new ChartPoint(Math.Min(1, (double)b.Second / _runSeconds), value(b) / scale))];
    }

    private void ClearResults()
    {
        Result = null;
        ExportMessage = null;
        ElapsedText = Note = "";
        Throughput = AverageText = P95Text = ErrorRateText = ThroughputMax = ResponseTimeMax = "";
        ActiveUsers = 0;
        TotalRequests = 0;
        ThroughputPoints = ResponseTimePoints = ErrorPoints = UserPoints = [];
        RequestRows = [];
        ErrorMessages = [];
    }

    private static string Ms(double ms) => ResponseText.FormatDuration(TimeSpan.FromMilliseconds(ms));

    private static string Clock(TimeSpan t) =>
        string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalMinutes}:{t.Seconds:00}");
}
