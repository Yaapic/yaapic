using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Runner;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Shell;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// Mock servers and load tests end to end: real sockets on the loopback interface, the real transport of the request editor,
/// real files and stores. Nothing here uses a fixed delay; every wait is on a condition.
/// </summary>
public sealed class Milestone6MocksAndLoadEndToEndTests : IDisposable
{
    private readonly ScriptedShellFixture _fx = new();
    private readonly List<ShellViewModel> _shells = [];

    public void Dispose()
    {
        foreach (var shell in _shells)
        {
            shell.RequestCloseAsync().GetAwaiter().GetResult();
        }

        _fx.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int FreePort() => TestPorts.Next();

    private static bool PortIsFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 400 && !condition(); i++)
        {
            await Task.Delay(25, Ct);
        }

        Assert.True(condition(), "The condition was not met in time.");
    }

    private async Task<ShellViewModel> NewShellAsync()
    {
        var shell = await _fx.NewShellAsync();
        _shells.Add(shell);
        await shell.Mocks!.LoadAsync();
        return shell;
    }

    private static void AddRoute(ShellViewModel shell, string name, string method, string path, int status, string body, int delayMs = 0, bool enabled = true, params KeyValueEntry[] headers)
    {
        var mocks = shell.Mocks!;
        mocks.AddRouteCommand.Execute(null);
        var route = mocks.SelectedRoute!;
        route.Name = name;
        route.Method = method;
        route.Path = path;
        route.Status = status;
        route.Body = body;
        route.DelayMs = delayMs;
        route.Enabled = enabled;
        route.Headers.Load(headers);
    }

    private static async Task<string> StartServerAsync(ShellViewModel shell, int port, Action? routes = null, bool cors = true)
    {
        var mocks = shell.Mocks!;
        await mocks.NewServerCommand.ExecuteAsync(null);
        mocks.Name = "Mock " + port;
        mocks.Port = port;
        mocks.Cors = cors;
        routes?.Invoke();
        await mocks.SaveCommand.ExecuteAsync(null);
        Assert.Empty(mocks.ValidationErrors);
        await mocks.StartCommand.ExecuteAsync(null);
        Assert.Null(mocks.Error);
        Assert.True(mocks.SelectedIsRunning);
        return "http://127.0.0.1:" + port;
    }

    private static async Task<RequestEditorViewModel> SendAsync(ShellViewModel shell, HttpMethodKind method, string url, string? body = null, params KeyValueEntry[] headers)
    {
        var tab = shell.Tabs.Tabs[0];
        tab.Method = method;
        tab.Url = url;
        tab.Headers.Load(headers);
        tab.BodyKind = body is null ? BodyKind.None : BodyKind.Raw;
        if (body is not null)
        {
            tab.BodyContentType = RawContentType.Json;
            tab.BodyText = body;
        }

        await tab.SendCommand.ExecuteAsync(null);
        Assert.Equal(ResponseState.Completed, tab.Response.State);
        return tab;
    }

    // The response panel pretty-prints JSON bodies.
    private static string Compact(RequestEditorViewModel tab) => string.Concat(tab.Response.BodyText.Where(c => !char.IsWhiteSpace(c)));

    private static string? Header(RequestEditorViewModel tab, string name) =>
        tab.Response.Headers.FirstOrDefault(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;

    private static void StandardRoutes(ShellViewModel shell)
    {
        AddRoute(
            shell, "User", "GET", "/users/:id", 200, "{\"id\":\"{{params.id}}\"}", 0, true,
            new KeyValueEntry("Content-Type", "application/json"), new KeyValueEntry("X-User-Id", "{{params.id}}"));
        AddRoute(shell, "Echo", "POST", "/echo", 201, "{{body}}");
        AddRoute(shell, "Files", "ANY", "/files/*", 200, "file:{{params.*}}");
        AddRoute(shell, "Off", "GET", "/off", 200, "never", 0, false);
        AddRoute(shell, "Slow", "GET", "/slow", 200, "slow", 200);
    }

    [Fact]
    public async Task RoutesAnswerRealRequestsWithTemplatesCorsDelayAndNotFound()
    {
        var shell = await NewShellAsync();
        var url = await StartServerAsync(shell, FreePort(), () => StandardRoutes(shell));

        var user = await SendAsync(shell, HttpMethodKind.Get, url + "/users/42");
        Assert.StartsWith("200", user.Response.StatusText, StringComparison.Ordinal);
        Assert.Equal("{\"id\":\"42\"}", Compact(user));
        Assert.Equal("42", Header(user, "X-User-Id"));
        Assert.Equal("*", Header(user, "Access-Control-Allow-Origin"));

        var echo = await SendAsync(shell, HttpMethodKind.Post, url + "/echo", "{\"a\":1}");
        Assert.StartsWith("201", echo.Response.StatusText, StringComparison.Ordinal);
        Assert.Equal("{\"a\":1}", Compact(echo));

        var file = await SendAsync(shell, HttpMethodKind.Delete, url + "/files/a/b.txt");
        Assert.Equal("file:a/b.txt", file.Response.BodyText);

        var off = await SendAsync(shell, HttpMethodKind.Get, url + "/off");
        Assert.StartsWith("404", off.Response.StatusText, StringComparison.Ordinal);
        Assert.Contains("No mock route matches GET /off", off.Response.BodyText, StringComparison.Ordinal);

        var preflight = await SendAsync(
            shell, HttpMethodKind.Options, url + "/anything", null,
            new KeyValueEntry("Access-Control-Request-Method", "GET"), new KeyValueEntry("Origin", "http://example.test"));
        Assert.StartsWith("204", preflight.Response.StatusText, StringComparison.Ordinal);
        Assert.Equal("*", Header(preflight, "Access-Control-Allow-Origin"));
        Assert.Contains("GET", Header(preflight, "Access-Control-Allow-Methods"), StringComparison.Ordinal);

        var head = await SendAsync(shell, HttpMethodKind.Head, url + "/users/42");
        Assert.StartsWith("200", head.Response.StatusText, StringComparison.Ordinal);
        Assert.Equal("", head.Response.BodyText);

        var watch = Stopwatch.StartNew();
        var slow = await SendAsync(shell, HttpMethodKind.Get, url + "/slow");
        watch.Stop();
        Assert.Equal("slow", slow.Response.BodyText);
        Assert.True(watch.ElapsedMilliseconds >= 190, $"The delayed route answered after {watch.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public async Task TheLogListsRequestsWithRouteNamesAndAnEditedRouteChangesTheNextAnswer()
    {
        var shell = await NewShellAsync();
        var url = await StartServerAsync(shell, FreePort(), () => StandardRoutes(shell));
        var mocks = shell.Mocks!;

        await SendAsync(shell, HttpMethodKind.Get, url + "/users/7");
        await SendAsync(shell, HttpMethodKind.Get, url + "/nope");
        await WaitUntilAsync(() => mocks.Log.Count == 2);

        Assert.Equal("/nope", mocks.Log[0].Target);
        Assert.Equal(404, mocks.Log[0].Status);
        Assert.Equal("/users/7", mocks.Log[1].Target);
        Assert.Equal("User", mocks.Log[1].Route);
        Assert.Equal("GET", mocks.Log[1].Method);

        mocks.SelectedRoute = mocks.Routes.Single(r => r.Name == "User");
        mocks.SelectedRoute.Body = "{\"edited\":\"{{params.id}}\"}";
        Assert.True(mocks.IsDirty);
        await mocks.SaveCommand.ExecuteAsync(null);
        Assert.False(mocks.IsDirty);

        var next = await SendAsync(shell, HttpMethodKind.Get, url + "/users/8");
        Assert.Equal("{\"edited\":\"8\"}", Compact(next));

        var file = Directory.EnumerateFiles(_fx.PathOf("mocks"), "*.mock.yaml").Single();
        var yaml = await File.ReadAllTextAsync(file, Ct);
        Assert.Contains("/users/:id", yaml, StringComparison.Ordinal);
        Assert.Contains("edited", yaml, StringComparison.Ordinal);
        Assert.Contains("/files/*", yaml, StringComparison.Ordinal);
        Assert.Contains("enabled: false", yaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GeneratingFromAFolderCreatesTheRoutes()
    {
        _fx.WriteFolder("api", "Api");
        _fx.WriteRequest("api", "Ping", "{{baseUrl}}/ping");
        _fx.WriteRequest("api", "Items", "{{baseUrl}}/items");
        var shell = await NewShellAsync();
        var folder = shell.Collections.Roots.First(r => r.IsFolder);

        await shell.MockFromFolderCommand.ExecuteAsync(folder);

        var mocks = shell.Mocks!;
        Assert.NotNull(mocks.SelectedServer);
        Assert.Equal(["/items", "/ping"], mocks.Routes.Select(r => r.Path).Order());
        Assert.All(mocks.Routes, r => Assert.Equal("GET", r.Method));
        Assert.Single(Directory.EnumerateFiles(_fx.PathOf("mocks"), "*.mock.yaml"));
    }

    [Fact]
    public async Task StoppingAndClosingTheShellFreeThePorts()
    {
        var shell = await NewShellAsync();
        var first = FreePort();
        await StartServerAsync(shell, first, () => AddRoute(shell, "Home", "GET", "/", 200, "ok"));
        Assert.False(PortIsFree(first));
        Assert.Equal(1, shell.Mocks!.RunningCount);

        await shell.Mocks.StopCommand.ExecuteAsync(null);

        Assert.False(shell.Mocks.SelectedIsRunning);
        Assert.Equal(0, shell.Mocks.RunningCount);
        Assert.True(PortIsFree(first));

        var second = FreePort();
        await StartServerAsync(shell, second, () => AddRoute(shell, "Home", "GET", "/", 200, "ok"));
        Assert.False(PortIsFree(second));

        _shells.Remove(shell);
        Assert.True(await shell.RequestCloseAsync());

        Assert.True(PortIsFree(second));
    }

    // A folder whose requests target {{baseUrl}} (the Dev environment), a running mock on that URL.
    private async Task<(ShellViewModel Shell, string Url)> NewTargetAsync(bool closeConnections, params (string Name, string Path)[] requests)
    {
        var port = FreePort();
        var url = "http://127.0.0.1:" + port;
        _fx.WriteEnvironment("Dev", ("baseUrl", url));
        _fx.WriteFolder("run", "Run");
        foreach (var (name, path) in requests)
        {
            // The mock host caps open connections at 100 and has no idle timeout, so a load test that keeps connections alive would
            // run into its 503 answers; Connection: close keeps the count flat.
            _fx.WriteRequest("run", name, "{{baseUrl}}" + path, headers: closeConnections ? ("Connection", "close") : null);
        }

        var shell = await NewShellAsync();
        shell.Environments.ActiveEnvironment = shell.Environments.Environments.Single(e => e.Name == "Dev");
        await StartServerAsync(shell, port, () =>
        {
            AddRoute(shell, "User", "GET", "/users/:id", 200, "{\"id\":\"{{params.id}}\"}");
            AddRoute(shell, "Boom", "GET", "/boom", 500, "{\"error\":true}");
        });
        return (shell, url);
    }

    private static CollectionNodeViewModel RunFolder(ShellViewModel shell) => shell.Collections.Roots.First(r => r.IsFolder && r.Name == "Run");

    [Fact]
    public async Task TheRunnerRunsAFolderAgainstAMock()
    {
        var (shell, _) = await NewTargetAsync(false, ("U1", "/users/1"), ("U2", "/users/2"));
        var runner = shell.Runner!;
        runner.Open(RunFolder(shell).Node);

        await runner.RunCommand.ExecuteAsync(null);

        Assert.Equal(RunStatus.Completed, runner.Result!.Status);
        Assert.Equal(["U1", "U2"], runner.Rows.Select(r => r.Name));
        Assert.All(runner.Rows, r => Assert.Equal(RunRequestOutcome.Passed, r.Outcome));
        Assert.All(runner.Rows, r => Assert.Equal(200, r.Result.StatusCode));
        await WaitUntilAsync(() => shell.Mocks!.Log.Count == 2);
    }

    private static string Statuses(ShellViewModel shell) =>
        string.Join(", ", shell.Mocks!.Log.GroupBy(l => l.Status).Select(g => $"{g.Key} x{g.Count()}"));

    private static LoadTestResult ResultOf(ShellViewModel shell) => shell.LoadTest!.Result!;

    [Fact]
    public async Task AFixedLoadTestAgainstTheMockCountsRequestsWithoutErrorsAndExportsAReport()
    {
        var (shell, _) = await NewTargetAsync(true, ("U1", "/users/1"));
        var load = shell.LoadTest!;
        load.Open(RunFolder(shell).Node);
        load.Profile = LoadProfile.Fixed;
        load.VirtualUsers = 5;
        load.DurationSeconds = 10;
        load.ThinkTimeMs = 100;
        _fx.Dialogs.ConfirmAnswer = true;
        _fx.Dialogs.SaveFileAnswer = true;

        await load.StartCommand.ExecuteAsync(null);

        var result = ResultOf(shell);
        Assert.Single(_fx.Dialogs.ConfirmMessages);
        Assert.Contains("127.0.0.1", _fx.Dialogs.ConfirmMessages[0], StringComparison.Ordinal);
        Assert.False(result.Canceled);
        Assert.True(result.Overall.Count > 0);
        Assert.True(result.Overall.Errors == 0, $"{result.Overall.Errors} of {result.Overall.Count} requests failed: {string.Join(" | ", result.ErrorMessages)}; the mock logged {Statuses(shell)}");
        Assert.Equal(0, result.Overall.ErrorRate);
        Assert.True(result.Buckets.Count >= 5, "Expected per-second buckets.");
        Assert.True(result.Buckets.Sum(b => b.Requests) > 0);
        Assert.Equal(5, result.Buckets.Max(b => b.ActiveUsers));
        Assert.Equal("0.0 %", load.ErrorRateText);

        await load.ExportCommand.ExecuteAsync(null);
        var saved = Assert.Single(_fx.Dialogs.SavedFiles);
        using var json = JsonDocument.Parse(saved.Content);
        Assert.Equal("yaapic-loadtest/1", json.RootElement.GetProperty("schema").GetString());
        Assert.Equal(result.Overall.Count, json.RootElement.GetProperty("overall").GetProperty("count").GetInt64());
        Assert.Equal(result.Buckets.Count, json.RootElement.GetProperty("buckets").GetArrayLength());
    }

    [Fact]
    public async Task ARampUpLoadTestRaisesTheActiveUsers()
    {
        var (shell, _) = await NewTargetAsync(true, ("U1", "/users/1"));
        var load = shell.LoadTest!;
        load.Open(RunFolder(shell).Node);
        load.Profile = LoadProfile.RampUp;
        load.VirtualUsers = 10;
        load.RampUpSeconds = 5;
        load.DurationSeconds = 10;
        load.ThinkTimeMs = 400;
        _fx.Dialogs.ConfirmAnswer = true;

        await load.StartCommand.ExecuteAsync(null);

        var result = ResultOf(shell);
        Assert.True(result.Overall.Count > 0);
        Assert.True(result.Overall.Errors == 0, $"{result.Overall.Errors} of {result.Overall.Count} requests failed: {string.Join(" | ", result.ErrorMessages)}; the mock logged {Statuses(shell)}");
        var users = result.Buckets.Select(b => b.ActiveUsers).ToArray();
        Assert.True(users[0] < users.Max(), "Active users should rise during the ramp.");
        Assert.Equal(10, users.Max());
    }

    [Fact]
    public async Task ARouteReturning500GivesAFullErrorRate()
    {
        var (shell, _) = await NewTargetAsync(true, ("Boom", "/boom"));
        var load = shell.LoadTest!;
        load.Open(RunFolder(shell).Node);
        load.VirtualUsers = 2;
        load.DurationSeconds = 10;
        load.ThinkTimeMs = 100;
        _fx.Dialogs.ConfirmAnswer = true;

        await load.StartCommand.ExecuteAsync(null);

        var result = ResultOf(shell);
        Assert.True(result.Overall.Count > 0);
        Assert.Equal(result.Overall.Count, result.Overall.Errors);
        Assert.Equal(1.0, result.Overall.ErrorRate);
        Assert.Equal("100.0 %", load.ErrorRateText);
    }

    [Fact]
    public async Task StopCancelsALoadTestEarly()
    {
        var (shell, _) = await NewTargetAsync(true, ("U1", "/users/1"));
        var load = shell.LoadTest!;
        load.Open(RunFolder(shell).Node);
        load.VirtualUsers = 2;
        load.DurationSeconds = 60;
        load.ThinkTimeMs = 100;
        _fx.Dialogs.ConfirmAnswer = true;

        var run = load.StartCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => load.IsRunning && load.TotalRequests > 0);
        load.StopCommand.Execute(null);
        await run;

        var result = ResultOf(shell);
        Assert.True(result.Canceled);
        Assert.True(result.Duration < TimeSpan.FromSeconds(30));
        Assert.True(result.Overall.Count > 0);
        Assert.False(load.IsRunning);
    }

    [Fact]
    public async Task DecliningTheConfirmationStartsNothing()
    {
        var (shell, _) = await NewTargetAsync(true, ("U1", "/users/1"));
        var load = shell.LoadTest!;
        load.Open(RunFolder(shell).Node);
        _fx.Dialogs.ConfirmAnswer = false;

        await load.StartCommand.ExecuteAsync(null);

        Assert.Single(_fx.Dialogs.ConfirmMessages);
        Assert.False(load.IsRunning);
        Assert.Null(load.Result);
        Assert.Empty(shell.Mocks!.Log);
    }
}
