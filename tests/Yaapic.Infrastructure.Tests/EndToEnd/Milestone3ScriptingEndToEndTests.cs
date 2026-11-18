using System.Diagnostics;
using Yaapic.Core.Scripting;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Shell;
using static Yaapic.Infrastructure.Tests.EndToEnd.ScriptedShellFixture;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// JavaScript scripting end to end: loopback server, shell view model, real QuickJS engine, real files and stores.
/// The natives come from <c>native/Build-Natives.ps1</c>; a missing library fails the tests instead of skipping them.
/// </summary>
public sealed class Milestone3ScriptingEndToEndTests : IClassFixture<LoopbackServer>, IDisposable
{
    private static readonly ScriptLimits ShortLimits = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(1) };

    private readonly LoopbackServer _server;
    private readonly ScriptedShellFixture _fx = new();

    public Milestone3ScriptingEndToEndTests(LoopbackServer server) => _server = server;

    public void Dispose() => _fx.Dispose();

    private string Url(string path) => _server.BaseUrl + path;

    private string WithBase(string script) => script.Replace("BASE", _server.BaseUrl, StringComparison.Ordinal);

    private static void ActivateEnvironment(ShellViewModel shell, string name) =>
        shell.Environments.ActiveEnvironment = shell.Environments.Environments.Single(e => e.Name == name);

    [Fact]
    public async Task PreRequestMutatesHeaderQueryAndUrlVariable()
    {
        var id = _fx.WriteRequest(
            "",
            "Mutate",
            Url("/{{seg}}?a=1"),
            pre: "yaapic.variables.set('seg', 'echo');\nyaapic.request.headers.add({ key: 'X-Added', value: 'yes' });\nyaapic.request.url.query.upsert({ key: 'a', value: '9' });");
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        var echo = Json(tab);
        var path = echo.GetProperty("path").GetString()!;
        Assert.StartsWith("/echo?", path, StringComparison.Ordinal);
        Assert.Contains("a=9", path, StringComparison.Ordinal);
        Assert.DoesNotContain("a=1", path, StringComparison.Ordinal);
        Assert.Equal("yes", echo.GetProperty("headers").GetProperty("X-Added").GetString());
    }

    [Fact]
    public async Task TokenSavedByOneRequestIsStoredOnDiskAndSentByTheNext()
    {
        _fx.WriteEnvironment("Dev");
        var a = _fx.WriteRequest("", "Login", Url("/json"), post: "yaapic.environment.set('token', yaapic.response.json().token);");
        var b = _fx.WriteRequest("", "Use token", Url("/headers?t={{token}}"), headers: ("Authorization", "Bearer {{token}}"));
        var shell = await _fx.NewShellAsync();
        ActivateEnvironment(shell, "Dev");
        var tabB = Open(shell, b);
        Assert.False(tabB.Variables.Tokenize(tabB.Url).Single().Hint.IsResolved);

        await Open(shell, a).SendCommand.ExecuteAsync(null);

        Assert.Contains("abc123", await File.ReadAllTextAsync(_fx.PathOf("environments", "dev.env.yaml"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
        var token = tabB.Variables.Tokenize(tabB.Url).Single();
        Assert.True(token.Hint.IsResolved);
        Assert.Equal("abc123", token.Hint.Value);

        await tabB.SendCommand.ExecuteAsync(null);

        Assert.Equal("Bearer abc123", Json(tabB).GetProperty("Authorization").GetString());
    }

    [Fact]
    public async Task CollectionFolderAndRequestScriptsRunInOrderForBothPhases()
    {
        _fx.WriteFolder("acme", "Acme", pre: "console.log('pre:collection');", post: "console.log('post:collection');");
        _fx.WriteFolder("acme/users", "Users", pre: "console.log('pre:folder');", post: "console.log('post:folder');");
        var id = _fx.WriteRequest("acme/users", "Ordered", Url("/echo"), pre: "console.log('pre:request');", post: "console.log('post:request');");
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(
            ["pre:collection", "pre:folder", "pre:request", "post:collection", "post:folder", "post:request"],
            Messages(shell, ConsoleLineKind.Log));
    }

    [Fact]
    public async Task FiveTestsFillTheResultsWithPassAndFailMessages()
    {
        var id = _fx.WriteRequest(
            "",
            "Tests",
            Url("/echo"),
            post: """
                yaapic.test('one', () => { yaapic.expect(1).to.equal(1); });
                yaapic.test('two', () => { yaapic.expect(yaapic.response.code).to.equal(200); });
                yaapic.test('three', () => { yaapic.expect([1, 2]).to.have.lengthOf(2); });
                yaapic.test('four', () => { yaapic.expect(1).to.equal(2); });
                yaapic.test('five', () => { yaapic.expect('abc').to.include('z'); });
                """);
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        var tests = tab.Response.Tests;
        Assert.Equal(3, tests.PassedCount);
        Assert.Equal(2, tests.FailedCount);
        Assert.Equal("3/5", tests.Summary);
        Assert.Equal("3 of 5 passed", tests.SummaryText);
        var failed = tests.Items.Where(t => !t.Passed).ToList();
        Assert.Equal(["four", "five"], failed.Select(t => t.Name));
        Assert.Contains("expected 1 to equal 2", failed[0].Message, StringComparison.Ordinal);
        Assert.Contains("include", failed[1].Message, StringComparison.Ordinal);
        Assert.Equal("/echo", Json(tab).GetProperty("path").GetString());
    }

    [Fact]
    public async Task ScriptErrorsAbortTheSendKeepTheResponseOrTimeOutWithoutLeakingThreads()
    {
        var throwing = _fx.WriteRequest("", "Throws", Url("/abort-marker"), pre: "let a = 1;\nthrow new Error('boom');");
        var postThrows = _fx.WriteRequest("", "Post throws", Url("/echo"), post: "throw new Error('after');");
        var loops = _fx.WriteRequest("", "Loops", Url("/loop-marker"), pre: "while (true) {}");
        var shell = await _fx.NewShellAsync(ShortLimits);
        var threadsBefore = Process.GetCurrentProcess().Threads.Count;

        var tab = Open(shell, throwing);
        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Failed, tab.Response.State);
        Assert.Contains("boom", tab.Response.ErrorMessage, StringComparison.Ordinal);
        var error = Assert.Single(tab.Response.Tests.Errors);
        Assert.Equal("line 2", error.Location);
        Assert.DoesNotContain(_server.Hits, h => h.StartsWith("/abort-marker", StringComparison.Ordinal));
        Assert.Contains(shell.Console.Lines, l => l.Kind == ConsoleLineKind.Error && l.Message.Contains("boom", StringComparison.Ordinal) && l.Message.Contains("line 2", StringComparison.Ordinal));

        var post = Open(shell, postThrows);
        await post.SendCommand.ExecuteAsync(null);

        Assert.Equal("/echo", Json(post).GetProperty("path").GetString());
        Assert.Contains("after", Assert.Single(post.Response.Tests.Errors).Message, StringComparison.Ordinal);

        var loop = Open(shell, loops);
        var watch = Stopwatch.StartNew();
        await loop.SendCommand.ExecuteAsync(null);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4), $"The loop took {watch.Elapsed}.");
        Assert.Equal(ResponseState.Failed, loop.Response.State);
        Assert.Equal("Timed out", Assert.Single(loop.Response.Tests.Errors).StatusText);
        Assert.DoesNotContain(_server.Hits, h => h.StartsWith("/loop-marker", StringComparison.Ordinal));

        loop.Scripts.PreRequest.Source = "";
        await loop.SendCommand.ExecuteAsync(null);

        Assert.Equal("/loop-marker", Json(loop).GetProperty("path").GetString());
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.InRange(Process.GetCurrentProcess().Threads.Count, 0, threadsBefore + 8);
    }

    [Fact]
    public async Task PmSendRequestFeedsTheMainRequestLogsNetworkLinesAndStopsAtTwentyCalls()
    {
        var id = _fx.WriteRequest(
            "",
            "Chained",
            Url("/echo"),
            pre: WithBase("""
                const first = await yaapic.sendRequest('BASE/json');
                yaapic.request.headers.add({ key: 'X-Token', value: first.json().token });
                yaapic.sendRequest('BASE/json', (err, res) => { yaapic.request.headers.add({ key: 'X-Callback', value: String(res.code) }); });
                for (let i = 0; i < 19; i++) {
                  yaapic.sendRequest('BASE/count', (err, res) => { if (err) { console.log('err:' + (err.message ?? err)); } });
                }
                """));
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        var echo = Json(tab);
        Assert.Equal("abc123", echo.GetProperty("headers").GetProperty("X-Token").GetString());
        Assert.Equal("200", echo.GetProperty("headers").GetProperty("X-Callback").GetString());
        Assert.Contains(shell.Console.Lines, l => l.Kind == ConsoleLineKind.Network && l.Message.Contains("/json", StringComparison.Ordinal));
        var errors = Messages(shell, ConsoleLineKind.Log);
        Assert.Contains("limit of 20", Assert.Single(errors, e => e.StartsWith("err:", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Equal(18, _server.Hits.Count(h => h.StartsWith("/count", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task ScriptsWriteToTheMatchingScopeFilesAndLocalVariablesDoNotSurvive()
    {
        _fx.WriteFolder("acme", "Acme");
        _fx.WriteEnvironment("Dev");
        var id = _fx.WriteRequest(
            "acme",
            "Scopes",
            Url("/echo"),
            pre: "yaapic.globals.set('g', '1');\nyaapic.collectionVariables.set('c', '2');\nyaapic.environment.set('e', '3');\nyaapic.variables.set('l', '4');",
            post: "console.log('l=' + yaapic.variables.get('l'));");
        var shell = await _fx.NewShellAsync();
        ActivateEnvironment(shell, "Dev");
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        var globals = await File.ReadAllTextAsync(_fx.PathOf("globals.yaml"), TestContext.Current.CancellationToken);
        var folder = await File.ReadAllTextAsync(_fx.PathOf("requests", "acme", "folder.yaml"), TestContext.Current.CancellationToken);
        var environment = await File.ReadAllTextAsync(_fx.PathOf("environments", "dev.env.yaml"), TestContext.Current.CancellationToken);
        Assert.Contains("key: g", globals, StringComparison.Ordinal);
        Assert.Contains("key: c", folder, StringComparison.Ordinal);
        Assert.Contains("key: e", environment, StringComparison.Ordinal);
        Assert.DoesNotContain("key: l", globals + folder + environment, StringComparison.Ordinal);
        Assert.Contains("l=4", Messages(shell, ConsoleLineKind.Log));

        tab.Scripts.PreRequest.Source = "";
        shell.Console.ClearCommand.Execute(null);
        await tab.SendCommand.ExecuteAsync(null);

        Assert.Contains("l=undefined", Messages(shell, ConsoleLineKind.Log));

        shell.Environments.ActiveEnvironment = null;
        tab.Scripts.PreRequest.Source = "yaapic.environment.set('x', '1');";
        await tab.SendCommand.ExecuteAsync(null);

        Assert.Contains(shell.Console.Lines, l => l.Kind == ConsoleLineKind.Warn && l.Message.Contains("No active environment", StringComparison.Ordinal));
        Assert.DoesNotContain("key: x", await File.ReadAllTextAsync(_fx.PathOf("environments", "dev.env.yaml"), TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CookiesAreVisibleInPostResponseOnlyAndTheJarIsNotAvailable()
    {
        var id = _fx.WriteRequest(
            "",
            "Cookies",
            Url("/set-cookie"),
            pre: "console.log('pre:' + yaapic.cookies.has('a') + yaapic.cookies.has('b'));",
            post: "console.log('post:' + yaapic.cookies.get('a') + yaapic.cookies.get('b'));\ntry { yaapic.cookies.jar(); } catch (e) { console.log('jar:' + e.message); }");
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(
            ["pre:falsefalse", "post:12", "jar:The cookie jar is not available in Yaapic yet"],
            Messages(shell, ConsoleLineKind.Log));
    }

    [Fact]
    public async Task ScriptsTypedInTheEditorSurviveARestartAndStillRun()
    {
        var id = _fx.WriteRequest("", "Persisted", Url("/echo"));
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);
        tab.Scripts.PreRequest.Source = "yaapic.request.headers.add({ key: 'X-Persisted', value: 'pre' });";
        tab.Scripts.PostResponse.Source = "console.log('persisted post');";

        Assert.True(await shell.Tabs.SaveAsync(tab));

        var yaml = await File.ReadAllTextAsync(_fx.PathOf("requests", "persisted.request.yaml"), TestContext.Current.CancellationToken);
        Assert.Contains("X-Persisted", yaml, StringComparison.Ordinal);
        Assert.Contains("persisted post", yaml, StringComparison.Ordinal);

        var restarted = await _fx.NewShellAsync();
        var reopened = Open(restarted, id);
        await reopened.SendCommand.ExecuteAsync(null);

        Assert.Equal("pre", Json(reopened).GetProperty("headers").GetProperty("X-Persisted").GetString());
        Assert.Contains("persisted post", Messages(restarted, ConsoleLineKind.Log));
    }

    [Fact]
    public async Task CancellingASlowScriptReturnsPromptlyWithRequestCanceled()
    {
        var id = _fx.WriteRequest("", "Slow", Url("/slow-marker"), pre: "while (true) {}");
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);

        var watch = Stopwatch.StartNew();
        var send = tab.SendCommand.ExecuteAsync(null);
        await Task.Delay(400, TestContext.Current.CancellationToken);
        tab.CancelCommand.Execute(null);
        await send;

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(3), $"The cancel took {watch.Elapsed}.");
        Assert.Equal(ResponseState.Failed, tab.Response.State);
        Assert.Equal("Request canceled", tab.Response.ErrorMessage);
        Assert.DoesNotContain(_server.Hits, h => h.StartsWith("/slow-marker", StringComparison.Ordinal));
    }
}
