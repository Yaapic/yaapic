using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class ScriptEngineRegistryTests
{
    private sealed class NullHost : IScriptHost
    {
        public ScriptInfo Info => new("prerequest", "r", Guid.Empty, "o");

        public ScriptRequest Request { get; } = new();

        public ScriptResponse? Response => null;

        public IReadOnlyList<ScriptCookie> ResponseCookies => [];

        public bool TryGetVariable(ScriptScope scope, string name, out string value)
        {
            value = "";
            return false;
        }

        public void SetVariable(ScriptScope scope, string name, string value)
        {
        }

        public bool UnsetVariable(ScriptScope scope, string name) => false;

        public void ClearVariables(ScriptScope scope)
        {
        }

        public IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope) => [];

        public string ReplaceIn(string template) => template;

        public ScriptSendResult Send(ScriptRequest request) => new(null, "n/a");
    }

    private static ScriptInvocation Invocation(ScriptLanguage language = ScriptLanguage.JavaScript) =>
        new(new ScriptBlock(language, "x"), ScriptPhase.PreRequest, "origin", new NullHost(), ScriptLimits.Default);

    [Fact]
    public void Returns_registered_engine()
    {
        var registry = new ScriptEngineRegistry();
        var engine = FakeScriptEngine.FromQueue();
        registry.Add(engine);
        Assert.Same(engine, registry.Get(ScriptLanguage.JavaScript));
        Assert.True(registry.IsAvailable(ScriptLanguage.JavaScript, out var reason));
        Assert.Null(reason);
    }

    [Fact]
    public void Second_add_replaces_first()
    {
        var registry = new ScriptEngineRegistry();
        var second = FakeScriptEngine.FromQueue();
        registry.Add(FakeScriptEngine.FromQueue());
        registry.Add(second);
        Assert.Same(second, registry.Get(ScriptLanguage.JavaScript));
    }

    [Fact]
    public async Task Missing_language_gives_unavailable_engine()
    {
        var registry = new ScriptEngineRegistry();
        registry.Add(FakeScriptEngine.FromQueue());
        Assert.False(registry.IsAvailable(ScriptLanguage.Lua, out var reason));
        Assert.Equal("No Lua engine is installed", reason);

        var result = await registry.Get(ScriptLanguage.Lua).RunAsync(Invocation(ScriptLanguage.Lua), CancellationToken.None);
        Assert.Equal(ScriptRunStatus.Unavailable, result.Status);
        Assert.Equal("No Lua engine is installed", result.Error!.Message);
        Assert.Equal(ScriptLanguage.Lua, result.Language);
    }

    [Fact]
    public void Add_null_throws() => Assert.Throws<ArgumentNullException>(() => new ScriptEngineRegistry().Add(null!));

    [Fact]
    public void Default_limits_match_adr()
    {
        var l = ScriptLimits.Default;
        Assert.Equal(TimeSpan.FromSeconds(5), l.Timeout);
        Assert.Equal(64L * 1024 * 1024, l.MemoryLimitBytes);
        Assert.Equal(20, l.MaxSendRequests);
        Assert.Equal(1000, l.MaxConsoleEntries);
        Assert.Equal(8192, l.MaxConsoleEntryLength);
    }

    [Fact]
    public async Task Fake_engine_records_invocations_in_order()
    {
        var engine = FakeScriptEngine.FromQueue(ScriptRunResult.Unavailable(ScriptPhase.PreRequest, "o", ScriptLanguage.JavaScript, "why"));
        var first = Invocation();
        var r1 = await engine.RunAsync(first, CancellationToken.None);
        var r2 = await engine.RunAsync(Invocation(), CancellationToken.None);
        Assert.Equal(ScriptRunStatus.Unavailable, r1.Status);
        Assert.True(r2.Succeeded);
        Assert.Equal(2, engine.Invocations.Count);
        Assert.Same(first, engine.Invocations[0]);
    }

    [Fact]
    public async Task Fake_engine_honours_cancellation_during_delay()
    {
        var engine = FakeScriptEngine.FromQueue();
        engine.Delay = TimeSpan.FromMinutes(1);
        using var cts = new CancellationTokenSource();
        var run = engine.RunAsync(Invocation(), cts.Token);
        await cts.CancelAsync();
        Assert.Equal(ScriptRunStatus.Canceled, (await run).Status);
    }
}
