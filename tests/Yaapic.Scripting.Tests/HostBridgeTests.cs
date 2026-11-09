using System.Text.Json;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Scripting.Tests;

public class HostBridgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private sealed class RecordingHost : IScriptHost
    {
        public Dictionary<(ScriptScope, string), string> Vars { get; } = [];

        public List<string> Calls { get; } = [];

        public ScriptInfo Info { get; } = new("test", "Users", Guid.Parse("11111111-1111-1111-1111-111111111111"), "post-response · Users");

        public ScriptRequest Request { get; set; } = new();

        public ScriptResponse? Response { get; set; }

        public IReadOnlyList<ScriptCookie> ResponseCookies { get; set; } = [];

        public ScriptSendResult SendResult { get; set; } = new(null, "boom");

        public ScriptRequest? Sent { get; private set; }

        public bool TryGetVariable(ScriptScope scope, string name, out string value) => Vars.TryGetValue((scope, name), out value!);

        public void SetVariable(ScriptScope scope, string name, string value) => Vars[(scope, name)] = value;

        public bool UnsetVariable(ScriptScope scope, string name) => Vars.Remove((scope, name));

        public void ClearVariables(ScriptScope scope)
        {
            foreach (var key in Vars.Keys.Where(k => k.Item1 == scope).ToList())
            {
                Vars.Remove(key);
            }

            Calls.Add($"clear:{scope}");
        }

        public IReadOnlyList<KeyValueEntry> ListVariables(ScriptScope scope) =>
            [.. Vars.Where(p => p.Key.Item1 == scope).Select(p => new KeyValueEntry(p.Key.Item2, p.Value))];

        public string ReplaceIn(string template) => template.Replace("{{a}}", "1", StringComparison.Ordinal);

        public ScriptSendResult Send(ScriptRequest request)
        {
            Sent = request;
            return SendResult;
        }
    }

    private static (HostBridge Bridge, RecordingHost Host, RunCollector Collector) Create(ScriptLimits? limits = null)
    {
        RecordingHost host = new();
        RunCollector collector = new(limits ?? ScriptLimits.Default, new FixedTime(), "post-response · Users");
        return (new(host, collector), host, collector);
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static JsonElement Ok(string result)
    {
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("ok", out var ok), result);
        return ok.Clone();
    }

    private static string Err(string result)
    {
        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("err", out var err), result);
        return err.GetString()!;
    }

    [Theory]
    [InlineData("layered", ScriptScope.Layered)]
    [InlineData("local", ScriptScope.Local)]
    [InlineData("environment", ScriptScope.Environment)]
    [InlineData("collection", ScriptScope.Collection)]
    [InlineData("globals", ScriptScope.Globals)]
    public void Variables_round_trip_in_every_scope(string name, ScriptScope scope)
    {
        var (bridge, host, _) = Create();

        Assert.Equal(JsonValueKind.Null, Ok(bridge.Invoke(HostOps.VarSet, $$"""{"scope":"{{name}}","name":"k","value":"v"}""")).ValueKind);
        Assert.Equal("v", host.Vars[(scope, "k")]);
        Assert.Equal("v", Ok(bridge.Invoke(HostOps.VarGet, $$"""{"scope":"{{name}}","name":"k"}""")).GetString());
        Assert.True(Ok(bridge.Invoke(HostOps.VarHas, $$"""{"scope":"{{name}}","name":"k"}""")).GetBoolean());
        var list = Ok(bridge.Invoke(HostOps.VarList, $$"""{"scope":"{{name}}"}"""));
        Assert.Equal("k", list[0].GetProperty("key").GetString());
        Assert.Equal("v", list[0].GetProperty("value").GetString());
        Assert.True(Ok(bridge.Invoke(HostOps.VarUnset, $$"""{"scope":"{{name}}","name":"k"}""")).GetBoolean());
        Assert.False(Ok(bridge.Invoke(HostOps.VarUnset, $$"""{"scope":"{{name}}","name":"k"}""")).GetBoolean());
        Assert.False(Ok(bridge.Invoke(HostOps.VarHas, $$"""{"scope":"{{name}}","name":"k"}""")).GetBoolean());
        Assert.Equal(JsonValueKind.Null, Ok(bridge.Invoke(HostOps.VarGet, $$"""{"scope":"{{name}}","name":"k"}""")).ValueKind);
    }

    [Fact]
    public void Clear_and_replace_reach_the_host()
    {
        var (bridge, host, _) = Create();
        host.Vars[(ScriptScope.Environment, "a")] = "1";

        bridge.Invoke(HostOps.VarClear, """{"scope":"environment"}""");

        Assert.Empty(host.Vars);
        Assert.Contains("clear:Environment", host.Calls);
        Assert.Equal("x=1", Ok(bridge.Invoke(HostOps.VarReplace, """{"scope":"layered","value":"x={{a}}"}""")).GetString());
    }

    [Fact]
    public void Request_get_returns_the_request_and_set_applies_the_whole_dto()
    {
        var (bridge, host, _) = Create();
        host.Request.Url = "https://a.test";
        host.Request.Headers.Add(new("X-A", "1", false));

        var get = Ok(bridge.Invoke(HostOps.ReqGet, "{}"));
        Assert.Equal("GET", get.GetProperty("method").GetString());
        Assert.Equal("https://a.test", get.GetProperty("url").GetString());
        Assert.False(get.GetProperty("headers")[0].GetProperty("enabled").GetBoolean());

        Ok(bridge.Invoke(HostOps.ReqSet, """
            {"method":"POST","url":"https://b.test/x","headers":[{"key":"H","value":"v"}],"bodyMode":"urlencoded",
             "bodyText":"t","bodyContentType":"json","urlEncoded":[{"key":"f","value":"1"}]}
            """));

        Assert.Equal("POST", host.Request.Method);
        Assert.Equal("https://b.test/x", host.Request.Url);
        Assert.Equal([new KeyValueEntry("H", "v")], host.Request.Headers);
        Assert.Equal("urlencoded", host.Request.BodyMode);
        Assert.Equal("t", host.Request.BodyText);
        Assert.Equal("json", host.Request.BodyContentType);
        Assert.Equal([new KeyValueEntry("f", "1")], host.Request.UrlEncoded);
    }

    [Fact]
    public void Request_set_on_a_read_only_request_is_an_error()
    {
        var (bridge, host, _) = Create();
        host.Request = ScriptRequest.FromDefinition(HttpRequestDefinition.Create("r"), readOnly: true);

        var message = Err(bridge.Invoke(HostOps.ReqSet, """{"method":"GET","url":"u","headers":[],"bodyMode":"none","bodyText":"","bodyContentType":"text","urlEncoded":[]}"""));

        Assert.Contains("read-only", message);
    }

    [Fact]
    public void Response_get_is_null_without_a_response_and_complete_with_one()
    {
        var (bridge, host, _) = Create();
        Assert.Equal(JsonValueKind.Null, Ok(bridge.Invoke(HostOps.ResGet, "{}")).ValueKind);

        host.Response = new(201, "Created", [new("A", "b")], "body", 4, 12, "https://a.test", [Cookie()]);
        var res = Ok(bridge.Invoke(HostOps.ResGet, "{}"));

        Assert.Equal(201, res.GetProperty("code").GetInt32());
        Assert.Equal("Created", res.GetProperty("status").GetString());
        Assert.Equal("b", res.GetProperty("headers")[0].GetProperty("value").GetString());
        Assert.Equal("body", res.GetProperty("text").GetString());
        Assert.Equal(4, res.GetProperty("size").GetInt64());
        Assert.Equal(12, res.GetProperty("time").GetInt64());
        Assert.Equal("https://a.test", res.GetProperty("url").GetString());
        Assert.Equal("sid", res.GetProperty("cookies")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Info_returns_the_host_data()
    {
        var (bridge, _, _) = Create();
        var info = Ok(bridge.Invoke(HostOps.InfoGet, "{}"));

        Assert.Equal("test", info.GetProperty("eventName").GetString());
        Assert.Equal("Users", info.GetProperty("requestName").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", info.GetProperty("requestId").GetString());
        Assert.Equal("post-response · Users", info.GetProperty("origin").GetString());
    }

    [Fact]
    public void Console_lines_are_collected_with_level_and_origin()
    {
        var (bridge, _, collector) = Create();
        bridge.Invoke(HostOps.Console, """{"level":"warn","text":"careful"}""");
        bridge.Invoke(HostOps.Console, """{"level":"nonsense","text":"x"}""");

        Assert.Equal(
            [
                new ConsoleEntry(Now, ConsoleLevel.Warn, ConsoleSource.Script, "careful", "post-response · Users"),
                new ConsoleEntry(Now, ConsoleLevel.Log, ConsoleSource.Script, "x", "post-response · Users"),
            ],
            collector.Console);
    }

    [Fact]
    public void Plain_console_lines_split_the_level_from_the_text_at_the_first_newline()
    {
        var (bridge, _, collector) = Create();

        bridge.Invoke(HostOps.ConsolePlain, "warn\nline one\nline two");
        bridge.Invoke(HostOps.ConsolePlain, "nonsense\nx");

        Assert.Equal(["line one\nline two", "x"], collector.Console.Select(e => e.Message));
        Assert.Equal([ConsoleLevel.Warn, ConsoleLevel.Log], collector.Console.Select(e => e.Level));
    }

    [Fact]
    public void Console_truncates_long_entries_and_caps_the_entry_count()
    {
        var (bridge, _, collector) = Create(ScriptLimits.Default with { MaxConsoleEntries = 2, MaxConsoleEntryLength = 5 });

        bridge.Invoke(HostOps.Console, """{"level":"log","text":"abcdefgh"}""");
        bridge.Invoke(HostOps.Console, """{"level":"log","text":"abc"}""");
        bridge.Invoke(HostOps.Console, """{"level":"log","text":"dropped"}""");
        bridge.Invoke(HostOps.Console, """{"level":"log","text":"dropped too"}""");

        Assert.Equal(["abcd…", "abc", "Console output truncated"], collector.Console.Select(e => e.Message));
        Assert.Equal(ConsoleLevel.Warn, collector.Console[2].Level);
        Assert.Equal(ConsoleSource.System, collector.Console[2].Source);
    }

    [Fact]
    public void Test_results_are_recorded()
    {
        var (bridge, _, collector) = Create();
        bridge.Invoke(HostOps.Test, """{"name":"ok","passed":true,"message":null,"ms":1.5}""");
        bridge.Invoke(HostOps.Test, """{"name":"ko","passed":false,"message":"expected 1","ms":0}""");

        Assert.Equal(
            [
                new Domain.Scripting.TestResult("ok", true, null, TimeSpan.FromMilliseconds(1.5), "post-response · Users"),
                new Domain.Scripting.TestResult("ko", false, "expected 1", TimeSpan.Zero, "post-response · Users"),
            ],
            collector.Tests);
    }

    [Fact]
    public void Send_returns_the_response_or_the_error()
    {
        var (bridge, host, _) = Create();
        const string Args = """{"method":"PUT","url":"https://s.test","headers":[],"bodyMode":"raw","bodyText":"{}","bodyContentType":"json","urlEncoded":[]}""";

        var failure = Ok(bridge.Invoke(HostOps.Send, Args));
        Assert.Equal("boom", failure.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, failure.GetProperty("response").ValueKind);
        Assert.Equal("PUT", host.Sent!.Method);
        Assert.Equal("{}", host.Sent.BodyText);
        Assert.False(host.Sent.IsReadOnly);

        host.SendResult = new(new(200, "OK", [], "hi", 2, 3, "https://s.test", []), null);
        var success = Ok(bridge.Invoke(HostOps.Send, Args));
        Assert.Equal(200, success.GetProperty("response").GetProperty("code").GetInt32());
        Assert.Equal(JsonValueKind.Null, success.GetProperty("error").ValueKind);
    }

    [Fact]
    public void Cookies_are_returned()
    {
        var (bridge, host, _) = Create();
        Assert.Equal(0, Ok(bridge.Invoke(HostOps.Cookies, "{}")).GetArrayLength());

        host.ResponseCookies = [Cookie()];
        var cookie = Ok(bridge.Invoke(HostOps.Cookies, "{}"))[0];

        Assert.Equal("sid", cookie.GetProperty("name").GetString());
        Assert.Equal("abc", cookie.GetProperty("value").GetString());
        Assert.Equal("/", cookie.GetProperty("path").GetString());
        Assert.True(cookie.GetProperty("httpOnly").GetBoolean());
        Assert.Equal(JsonValueKind.Null, cookie.GetProperty("domain").ValueKind);
    }

    [Fact]
    public void Unsupported_names_the_member()
    {
        var (bridge, _, _) = Create();
        Assert.Equal("pm.vault is not supported in Yaapic", Err(bridge.Invoke(HostOps.Unsupported, "\"vault\"")));
    }

    [Theory]
    [InlineData(HostOps.VarGet, "{not json")]
    [InlineData(HostOps.VarGet, "null")]
    [InlineData(HostOps.VarGet, """{"scope":"layered"}""")]
    [InlineData(HostOps.VarGet, """{"scope":"bogus","name":"a"}""")]
    [InlineData(HostOps.ReqSet, "[]")]
    [InlineData(HostOps.Unsupported, "{}")]
    [InlineData("no.such.op", "{}")]
    public void Bad_calls_return_err_and_never_throw(string op, string args) =>
        Assert.False(string.IsNullOrEmpty(Err(Create().Bridge.Invoke(op, args))));

    private static ScriptCookie Cookie() => new("sid", "abc", null, "/", null, true, false, null);
}
