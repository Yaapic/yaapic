using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Yaapic.Domain.Monitors;
using Yaapic.Infrastructure.Storage;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Monitors;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// Monitors end to end: the real shell composition with a manual clock, loopback servers, real QuickJS and real files.
/// The scheduler is moved by advancing the clock; every wait is on a condition, never on a fixed delay.
/// </summary>
public sealed class Milestone6MonitorsEndToEndTests : IDisposable
{
    private const string PassScript = "yaapic.test('ok', () => yaapic.expect(yaapic.response.code).to.equal(200));";

    private readonly ScriptedShellFixture _fx = new();
    private readonly LoopbackServer _server = new();
    private readonly GateServer _gate = new();
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero));
    private readonly List<ShellViewModel> _shells = [];

    public void Dispose()
    {
        _gate.Dispose();
        foreach (var shell in _shells)
        {
            shell.RequestCloseAsync().GetAwaiter().GetResult();
        }

        _server.Dispose();
        _fx.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(25, Ct);
        }

        Assert.True(condition(), "The condition was not met in time.");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var i = 0; i < 400 && !await condition(); i++)
        {
            await Task.Delay(25, Ct);
        }

        Assert.True(await condition(), "The condition was not met in time.");
    }

    private Task<IReadOnlyList<MonitorRun>> RunsAsync(Guid monitorId) => new FileMonitorRunStore(_fx.Root).ListAsync(monitorId, 50, Ct);

    private Task WaitForRunsAsync(Guid monitorId, int count) => WaitUntilAsync(async () => (await RunsAsync(monitorId)).Count >= count);

    private Guid WriteFolderWithRequest(string folder, string url, string? post = PassScript)
    {
        var id = _fx.WriteFolder(folder, folder);
        _fx.WriteRequest(folder, "R1", url, post: post);
        return id;
    }

    /// <summary>
    /// Writes the monitors, composes the shell and loads the window. An enabled monitor without history runs at the first tick,
    /// which happens as the shell starts; tests that look at the window state hold that run on the gate until the window loaded.
    /// </summary>
    private async Task<ShellViewModel> StartAsync(params MonitorDefinition[] monitors)
    {
        var store = new FileMonitorStore(_fx.Root);
        foreach (var monitor in monitors)
        {
            await store.SaveAsync(monitor, Ct);
        }

        var shell = await _fx.NewShellAsync(time: _clock);
        _shells.Add(shell);
        await shell.Monitors!.LoadAsync();
        return shell;
    }

    private static MonitorDefinition Monitor(string name, Guid folderId, int interval = 5, Guid? environmentId = null, bool enabled = true) =>
        new(Guid.NewGuid(), name, folderId, environmentId, interval, enabled);

    [Fact]
    public async Task AMonitorRunsAtStartThenOnlyOnceItsIntervalElapsed()
    {
        var folder = WriteFolderWithRequest("a", _server.BaseUrl + "/status/200");
        var monitor = Monitor("Smoke", folder);
        await StartAsync(monitor);

        await WaitForRunsAsync(monitor.Id, 1);
        var first = (await RunsAsync(monitor.Id)).Single();
        Assert.Equal(MonitorRunStatus.Passed, first.Status);
        Assert.Equal(1, first.TestsPassed);
        Assert.True(File.Exists(_fx.PathOf(".yaapic", "monitor-runs.jsonl")));

        // The next run is due 5 minutes after the first one; 4 minutes later it is not, whatever the ticks did.
        _clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Single(await RunsAsync(monitor.Id));

        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitForRunsAsync(monitor.Id, 2);
        Assert.Equal(2, (await RunsAsync(monitor.Id)).Count);
        Assert.Equal(2, _server.Hits.Count);
    }

    [Fact]
    public async Task AMonitorWithItsOwnEnvironmentSendsThatEnvironmentsBaseUrl()
    {
        using var other = new LoopbackServer();
        _fx.WriteEnvironment("Active", ("baseUrl", other.BaseUrl));
        var monitorEnv = _fx.WriteEnvironment("Monitor", ("baseUrl", _server.BaseUrl));
        var folder = WriteFolderWithRequest("a", "{{baseUrl}}/status/200");
        var monitor = Monitor("Smoke", folder, environmentId: monitorEnv);
        var shell = await StartAsync(monitor);
        shell.Environments.ActiveEnvironment = shell.Environments.Environments.Single(e => e.Name == "Active");

        await WaitForRunsAsync(monitor.Id, 1);

        Assert.Equal(MonitorRunStatus.Passed, (await RunsAsync(monitor.Id)).Single().Status);
        Assert.Equal(["/status/200"], _server.Hits);
        Assert.Empty(other.Hits);
    }

    [Fact]
    public async Task AFailingTestWarnsThenAFixedRunClearsTheStatus()
    {
        var folder = WriteFolderWithRequest("a", _gate.BaseUrl + "/gate");
        var monitor = Monitor("Smoke", folder);
        var shell = await StartAsync(monitor);

        _gate.Open(500);
        await WaitUntilAsync(() => shell.MonitorsStatusText.Length > 0);

        var failed = (await RunsAsync(monitor.Id)).Single();
        Assert.Equal(MonitorRunStatus.Failed, failed.Status);
        Assert.Equal(Strings.MonitorStatusBar(1), shell.MonitorsStatusText);
        Assert.Contains(ScriptedShellFixture.Messages(shell, ConsoleLineKind.Warn), m => m.Contains("Smoke", StringComparison.Ordinal));

        _fx.WriteRequest("a", "R1", _server.BaseUrl + "/status/200", post: PassScript);
        shell.Monitors!.RunNowCommand.Execute(null);
        await WaitUntilAsync(() => shell.MonitorsStatusText.Length == 0);

        Assert.Equal(MonitorRunStatus.Passed, (await RunsAsync(monitor.Id))[0].Status);
    }

    [Fact]
    public async Task ADisabledMonitorDoesNotRunOnScheduleButRunsOnRunNow()
    {
        var folder = WriteFolderWithRequest("a", _server.BaseUrl + "/status/200");
        var monitor = Monitor("Smoke", folder, enabled: false);
        var shell = await StartAsync(monitor);

        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Empty(await RunsAsync(monitor.Id));

        shell.Monitors!.RunNowCommand.Execute(null);
        await WaitForRunsAsync(monitor.Id, 1);

        Assert.Single(await RunsAsync(monitor.Id));
        Assert.Single(_server.Hits);
    }

    [Fact]
    public async Task DeletingTheMonitoredFolderGivesAnErrorRun()
    {
        var folder = WriteFolderWithRequest("a", _server.BaseUrl + "/status/200");
        var monitor = Monitor("Smoke", folder, enabled: false);
        var shell = await StartAsync(monitor);
        Directory.Delete(_fx.PathOf("requests", "a"), recursive: true);

        shell.Monitors!.RunNowCommand.Execute(null);
        await WaitForRunsAsync(monitor.Id, 1);

        var run = (await RunsAsync(monitor.Id)).Single();
        Assert.Equal(MonitorRunStatus.Error, run.Status);
        Assert.Equal(Strings.MonitorFolderMissing, run.Message);
        Assert.Empty(_server.Hits);
    }

    [Fact]
    public async Task TwoDueMonitorsNeverRunAtTheSameTime()
    {
        var first = Monitor("First", WriteFolderWithRequest("a", _gate.BaseUrl + "/gate"));
        var second = Monitor("Second", WriteFolderWithRequest("b", _gate.BaseUrl + "/gate"));
        var shell = await StartAsync(first, second);

        await WaitUntilAsync(() => _gate.Events.Count >= 1);

        Assert.Single(shell.Monitors!.Monitors, m => m.IsRunning);
        Assert.Equal(["start"], _gate.Events);

        _gate.Open();
        await WaitForRunsAsync(first.Id, 1);
        await WaitForRunsAsync(second.Id, 1);

        Assert.Equal(["start", "end", "start", "end"], _gate.Events);
    }

    [Fact]
    public async Task VariableChangesMadeByARunNeverReachTheEnvironmentFile()
    {
        var env = _fx.WriteEnvironment("Dev", ("baseUrl", _server.BaseUrl));
        var folder = WriteFolderWithRequest("a", "{{baseUrl}}/status/200", post: PassScript + "\nyaapic.environment.set('token', 'tok-4242'); yaapic.environment.set('baseUrl', 'http://changed.invalid');");
        var monitor = Monitor("Smoke", folder, environmentId: env);
        await StartAsync(monitor);
        var file = _fx.PathOf("environments", "dev.env.yaml");
        var before = await File.ReadAllTextAsync(file, Ct);

        await WaitForRunsAsync(monitor.Id, 1);

        Assert.Equal(MonitorRunStatus.Passed, (await RunsAsync(monitor.Id)).Single().Status);
        Assert.Equal(before, await File.ReadAllTextAsync(file, Ct));
    }

    [Fact]
    public async Task TheMonitorFileRoundTripsAndDeletingTheMonitorRemovesItsHistory()
    {
        var folder = WriteFolderWithRequest("a", _server.BaseUrl + "/status/200");
        var monitor = Monitor("Smoke", folder, enabled: false);
        var shell = await StartAsync(monitor);
        var vm = shell.Monitors!;
        var store = new FileMonitorStore(_fx.Root);

        vm.Name = "Renamed";
        vm.SelectedInterval = vm.IntervalChoices.Single(c => c.Minutes == 15);
        await vm.SaveCommand.ExecuteAsync(null);

        var file = Assert.Single(Directory.GetFiles(_fx.PathOf("monitors"), "*.monitor.yaml"));
        Assert.Contains("Renamed", await File.ReadAllTextAsync(file, Ct), StringComparison.Ordinal);
        Assert.Equal(monitor with { Name = "Renamed", IntervalMinutes = 15 }, Assert.Single(await store.ListAsync(Ct)));

        vm.RunNowCommand.Execute(null);
        await WaitForRunsAsync(monitor.Id, 1);
        _fx.Dialogs.DeleteAnswer = true;
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(await store.ListAsync(Ct));
        Assert.False(File.Exists(file));
        Assert.Empty(await RunsAsync(monitor.Id));
    }

    [Fact]
    public async Task ClosingTheShellStopsTheSchedulerDuringARun()
    {
        var folder = WriteFolderWithRequest("a", _gate.BaseUrl + "/gate");
        var monitor = Monitor("Smoke", folder);
        var shell = await StartAsync(monitor);
        _shells.Remove(shell);

        await WaitUntilAsync(() => _gate.Events.Count >= 1);

        Assert.True(await shell.RequestCloseAsync());
        _gate.Open();
        _clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Empty(await RunsAsync(monitor.Id));
    }

    /// <summary>A loopback server whose <c>/gate</c> route answers only once a test opens the gate, recording when requests start and end.</summary>
    private sealed class GateServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly TaskCompletionSource _open = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public GateServer()
        {
            BaseUrl = $"http://127.0.0.1:{TestPorts.Next()}";
            _listener.Prefixes.Add(BaseUrl + "/");
            _listener.Start();
            _ = Task.Run(AcceptAsync);
        }

        public string BaseUrl { get; }

        public ConcurrentQueue<string> Events { get; } = new();

        public int Status { get; private set; } = 200;

        public void Open(int status = 200)
        {
            Status = status;
            _open.TrySetResult();
        }

        public void Dispose()
        {
            Open();
            _listener.Close();
        }

        private async Task AcceptAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(context));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                Events.Enqueue("start");
                await _open.Task;
                Events.Enqueue("end");
                context.Response.StatusCode = Status;
            }
            finally
            {
                try
                {
                    context.Response.Close();
                }
                catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                {
                    // The client went away.
                }
            }
        }
    }
}
