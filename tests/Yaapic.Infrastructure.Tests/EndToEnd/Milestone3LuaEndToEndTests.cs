using System.Diagnostics;
using Yaapic.Core.Scripting;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Shell;
using static Yaapic.Infrastructure.Tests.EndToEnd.ScriptedShellFixture;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// Lua and mixed-language scripting end to end, mirroring <see cref="Milestone3ScriptingEndToEndTests"/> over the same
/// loopback server and fixture with the real Lua 5.4 and QuickJS engines.
/// </summary>
public sealed class Milestone3LuaEndToEndTests : IClassFixture<LoopbackServer>, IDisposable
{
    private const string Lua = "lua";
    private static readonly ScriptLimits ShortLimits = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(1) };

    private readonly LoopbackServer _server;
    private readonly ScriptedShellFixture _fx = new();

    public Milestone3LuaEndToEndTests(LoopbackServer server) => _server = server;

    public void Dispose() => _fx.Dispose();

    private string Url(string path) => _server.BaseUrl + path;

    private string WithBase(string script) => script.Replace("BASE", _server.BaseUrl, StringComparison.Ordinal);

    private static void ActivateEnvironment(ShellViewModel shell, string name) =>
        shell.Environments.ActiveEnvironment = shell.Environments.Environments.Single(e => e.Name == name);

    private static int ThreadCount() => Process.GetCurrentProcess().Threads.Count;

    [Fact]
    public async Task PreRequestMutatesHeaderQueryAndUrlVariable()
    {
        var id = _fx.WriteRequest(
            "",
            "Mutate",
            Url("/{{seg}}?a=1"),
            pre: "pm.variables.set('seg', 'echo')\npm.request.headers.add{ key = 'X-Added', value = 'yes' }\npm.request.url.query.upsert{ key = 'a', value = '9' }",
            preLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
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
        var a = _fx.WriteRequest("", "Login", Url("/json"), post: "pm.environment.set('token', pm.response.json().token)", postLanguage: Lua);
        var b = _fx.WriteRequest("", "Use token", Url("/headers?t={{token}}"), headers: ("Authorization", "Bearer {{token}}"));
        var shell = await _fx.NewShellAsync(withLua: true);
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
        _fx.WriteFolder("acme", "Acme", pre: "console.log('pre:collection')", post: "console.log('post:collection')", preLanguage: Lua, postLanguage: Lua);
        _fx.WriteFolder("acme/users", "Users", pre: "console.log('pre:folder')", post: "console.log('post:folder')", preLanguage: Lua, postLanguage: Lua);
        var id = _fx.WriteRequest("acme/users", "Ordered", Url("/echo"), pre: "console.log('pre:request')", post: "console.log('post:request')", preLanguage: Lua, postLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
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
                pm.test('one', function() pm.expect(1).to.equal(1) end)
                pm.test('two', function() pm.expect(pm.response.code).to.equal(200) end)
                pm.test('three', function() pm.expect({ 1, 2 }).to.have.length(2) end)
                pm.test('four', function() pm.expect(1).to.equal(2) end)
                pm.test('five', function() pm.expect('abc').to.include('z') end)
                """,
            postLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
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
        var throwing = _fx.WriteRequest("", "Throws", Url("/abort-marker"), pre: "local a = 1\nerror('boom')", preLanguage: Lua);
        var postThrows = _fx.WriteRequest("", "Post throws", Url("/echo"), post: "error('after')", postLanguage: Lua);
        var loops = _fx.WriteRequest("", "Loops", Url("/loop-marker"), pre: "while true do end", preLanguage: Lua);
        var shell = await _fx.NewShellAsync(ShortLimits, withLua: true);
        var threadsBefore = ThreadCount();

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
        Assert.InRange(ThreadCount(), 0, threadsBefore + 8);
    }

    [Fact]
    public async Task PmSendRequestReturnsResAndErrLogsNetworkLinesAndStopsAtTwentyCalls()
    {
        var id = _fx.WriteRequest(
            "",
            "Chained",
            Url("/echo"),
            pre: WithBase("""
                local first, firstErr = pm.sendRequest('BASE/json')
                console.log('firstErr:' .. tostring(firstErr))
                pm.request.headers.add{ key = 'X-Token', value = first.json().token }
                pm.sendRequest('BASE/json', function(err, res) pm.request.headers.add{ key = 'X-Callback', value = tostring(res.code) } end)
                for i = 1, 19 do
                  local res, err = pm.sendRequest('BASE/count')
                  if err then console.log('err:' .. tostring(err)) end
                end
                """),
            preLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        var echo = Json(tab);
        Assert.Equal("abc123", echo.GetProperty("headers").GetProperty("X-Token").GetString());
        Assert.Equal("200", echo.GetProperty("headers").GetProperty("X-Callback").GetString());
        Assert.Contains(shell.Console.Lines, l => l.Kind == ConsoleLineKind.Network && l.Message.Contains("/json", StringComparison.Ordinal));
        var logs = Messages(shell, ConsoleLineKind.Log);
        Assert.Contains("firstErr:nil", logs);
        Assert.Contains("limit of 20", Assert.Single(logs, e => e.StartsWith("err:", StringComparison.Ordinal)), StringComparison.Ordinal);
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
            pre: "pm.globals.set('g', '1')\npm.collectionVariables.set('c', '2')\npm.environment.set('e', '3')\npm.variables.set('l', '4')",
            post: "console.log('l=' .. tostring(pm.variables.get('l')))",
            preLanguage: Lua,
            postLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
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

        Assert.Contains("l=nil", Messages(shell, ConsoleLineKind.Log));

        shell.Environments.ActiveEnvironment = null;
        tab.Scripts.PreRequest.Source = "pm.environment.set('x', '1')";
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
            pre: "console.log('pre:' .. tostring(pm.cookies.has('a')) .. tostring(pm.cookies.has('b')))",
            post: "console.log('post:' .. pm.cookies.get('a') .. pm.cookies.get('b'))\nlocal ok, e = pcall(pm.cookies.jar)\nconsole.log('jar:' .. (tostring(e):gsub('^.-:%d+: ', '')))",
            preLanguage: Lua,
            postLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(
            ["pre:falsefalse", "post:12", "jar:The cookie jar is not available in Yaapic yet"],
            Messages(shell, ConsoleLineKind.Log));
    }

    [Fact]
    public async Task LuaScriptsTypedInTheEditorSurviveARestartAndStillRun()
    {
        var id = _fx.WriteRequest("", "Persisted", Url("/echo"), preLanguage: Lua, postLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
        var tab = Open(shell, id);
        tab.Scripts.PreRequest.Source = "pm.request.headers.add{ key = 'X-Persisted', value = 'pre' }";
        tab.Scripts.PostResponse.Source = "console.log('persisted post')";

        Assert.True(await shell.Tabs.SaveAsync(tab));

        var yaml = await File.ReadAllTextAsync(_fx.PathOf("requests", "persisted.request.yaml"), TestContext.Current.CancellationToken);
        Assert.Contains("language: lua", yaml, StringComparison.Ordinal);
        Assert.Contains("X-Persisted", yaml, StringComparison.Ordinal);
        Assert.Contains("persisted post", yaml, StringComparison.Ordinal);

        var restarted = await _fx.NewShellAsync(withLua: true);
        var reopened = Open(restarted, id);
        await reopened.SendCommand.ExecuteAsync(null);

        Assert.Equal("pre", Json(reopened).GetProperty("headers").GetProperty("X-Persisted").GetString());
        Assert.Contains("persisted post", Messages(restarted, ConsoleLineKind.Log));
    }

    [Fact]
    public async Task CancellingASlowLuaScriptReturnsPromptlyWithRequestCanceled()
    {
        var id = _fx.WriteRequest("", "Slow", Url("/slow-marker"), pre: "while true do end", preLanguage: Lua);
        var shell = await _fx.NewShellAsync(withLua: true);
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

    [Fact]
    public async Task MixedChainRunsCollectionFirstAndSharesVariablesAcrossLanguages()
    {
        _fx.WriteFolder(
            "acme",
            "Acme",
            pre: "pm.variables.set('shared', 'from-js');\nconsole.log('pre:collection:js');",
            post: "pm.test('status is 200', function() pm.expect(pm.response.code).to.equal(200) end)\nconsole.log('post:collection:lua')",
            postLanguage: Lua);
        _fx.WriteFolder(
            "acme/users",
            "Users",
            pre: "console.log('pre:folder:lua ' .. pm.variables.get('shared'))\npm.request.headers.add{ key = 'X-From-Lua', value = pm.variables.get('shared') }\npm.variables.set('lua_written', 'lua-value')",
            preLanguage: Lua);
        var id = _fx.WriteRequest(
            "acme/users",
            "Mixed",
            Url("/echo"),
            post: "pm.test('echo has the header and the Lua variable', () => {\n  pm.expect(pm.response.json().headers['X-From-Lua']).to.equal('from-js');\n  pm.expect(pm.variables.get('lua_written')).to.equal('lua-value');\n});\nconsole.log('post:request:js');");
        var shell = await _fx.NewShellAsync(withLua: true);
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal("from-js", Json(tab).GetProperty("headers").GetProperty("X-From-Lua").GetString());
        Assert.Equal(
            ["pre:collection:js", "pre:folder:lua from-js", "post:collection:lua", "post:request:js"],
            Messages(shell, ConsoleLineKind.Log));
        var tests = tab.Response.Tests;
        Assert.Equal(2, tests.PassedCount);
        Assert.Equal(0, tests.FailedCount);
        Assert.Equal(["status is 200", "echo has the header and the Lua variable"], tests.Items.Select(t => t.Name));
    }

    [Fact]
    public async Task MissingLuaLibraryAbortsLuaSendsWithEngineUnavailableWhileJavaScriptStillWorks()
    {
        var lua = _fx.WriteRequest("", "Lua only", Url("/lua-marker"), pre: "console.log('never')", preLanguage: Lua);
        var js = _fx.WriteRequest("", "Js only", Url("/echo"), pre: "pm.request.headers.add({ key: 'X-Js', value: 'ok' });");
        var shell = await _fx.NewShellAsync();

        Assert.Contains(shell.ScriptEngineWarnings, w => w.StartsWith("Lua", StringComparison.Ordinal));
        Assert.True(shell.HasScriptEngineWarnings);

        var luaTab = Open(shell, lua);
        await luaTab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Failed, luaTab.Response.State);
        Assert.Equal("Engine unavailable", Assert.Single(luaTab.Response.Tests.Errors).StatusText);
        Assert.DoesNotContain(_server.Hits, h => h.StartsWith("/lua-marker", StringComparison.Ordinal));

        var jsTab = Open(shell, js);
        await jsTab.SendCommand.ExecuteAsync(null);

        Assert.Equal("ok", Json(jsTab).GetProperty("headers").GetProperty("X-Js").GetString());
    }
}
