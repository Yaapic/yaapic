using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Scripting.Lua;
using Yaapic.Scripting.QuickJs;
using Yaapic.TestDoubles;

namespace Yaapic.Scripting.Tests.Conformance;

/// <summary>Runs conformance cases end to end through <see cref="ScriptedRequestSender"/>; shared by the JavaScript and Lua suites.</summary>
internal static class ConformanceRunner
{
    private const string DefaultBody = """{"data":[{"id":7,"name":"ann"},{"id":9,"name":"bob"}],"token":"abc123","count":2}""";

    /// <summary>One conformance case: the scripts, the request, the stub response and what must be observed.</summary>
    internal sealed record Case(string Name)
    {
        /// <summary>The language of the scripts.</summary>
        public ScriptLanguage Language { get; init; } = ScriptLanguage.JavaScript;

        /// <summary>An explicit script chain (language, origin, pre, post); null uses <see cref="Pre"/> and <see cref="Post"/> on a single step.</summary>
        public (ScriptLanguage Language, string Origin, string Pre, string Post)[]? Chain { get; init; }

        public string? Pre { get; init; }
        public string? Post { get; init; }
        public string Method { get; init; } = "GET";
        public string RequestBody { get; init; } = "";
        public int Status { get; init; } = 200;
        public string Reason { get; init; } = "OK";
        public string ReplyBody { get; init; } = DefaultBody;
        public string[] Env { get; init; } = [];
        public string[] Globals { get; init; } = [];
        public int Sends { get; init; } = 1;
        public double TimeoutSeconds { get; init; } = 10;

        /// <summary>Expected tests, as <c>name|P</c> or <c>name|F|message</c>; null skips the check.</summary>
        public string[]? Tests { get; init; }

        /// <summary>Expected console messages, in order; null skips the check.</summary>
        public string[]? Console { get; init; }

        /// <summary>Expected changes of the last send, as <c>Scope.name=value</c> (<c>&lt;unset&gt;</c> for a removal); null skips the check.</summary>
        public string[]? Changes { get; init; }

        /// <summary>Expected headers on the request that reached the transport, as <c>Name=value</c>.</summary>
        public string[] SentHeaders { get; init; } = [];

        public string? SentUrl { get; init; }

        /// <summary>A fragment of the error of the run that fails; null means every run must succeed.</summary>
        public string? Error { get; init; }

        /// <summary>Whether the failing run aborts the send (pre-request) instead of keeping the response (post-response).</summary>
        public bool Aborts { get; init; }
    }

    internal static HttpResponseData Reply(Case c) =>
        new(c.Status, c.Reason, [new("Content-Type", "application/json")], [.. Encoding.UTF8.GetBytes(c.ReplyBody)], TimeSpan.FromMilliseconds(5), "https://api.example.com/users");

    private static List<KeyValueEntry> Pairs(IEnumerable<string> pairs) =>
        [.. pairs.Select(p => p.Split('=', 2)).Select(a => new KeyValueEntry(a[0], a[1]))];

    private static string Label(VariableChange c) => $"{c.Scope}.{c.Name}={c.Value ?? "<unset>"}";

    /// <summary>Applies the persisted changes of one send to the scopes of the next one.</summary>
    private static VariableScopes Apply(VariableScopes scopes, IReadOnlyList<VariableChange> changes)
    {
        List<KeyValueEntry> env = [.. scopes.Environment!.Variables], collection = [.. scopes.Collection], globals = [.. scopes.Globals];
        foreach (var change in changes)
        {
            var list = change.Scope switch
            {
                VariableScopeKind.Environment => env,
                VariableScopeKind.Collection => collection,
                _ => globals,
            };
            list.RemoveAll(e => e.Key == change.Name);
            if (change.Value is not null)
            {
                list.Add(new(change.Name, change.Value));
            }
        }

        return new(scopes.Environment with { Variables = env }, collection, globals);
    }

    internal static async Task<(ScriptedSendReport Report, FakeHttpTransport Transport)> Run(Case c, string? pre = null, string? post = null)
    {
        ScriptEngineRegistry registry = new();
        registry.Add(new QuickJsEngine());
        registry.Add(new LuaEngine());
        FakeHttpTransport transport = new((r, _) => Task.FromResult(SendResult.Success(Reply(c))));
        ScriptedRequestSender sender = new(new RequestSender(transport), registry, TimeProvider.System);
        var body = c.RequestBody.Length == 0 ? RequestBody.None : RequestBody.Raw(RawContentType.Json, c.RequestBody);
        var request = HttpRequestDefinition.Create("Users API") with
        {
            Url = "https://api.example.com/users",
            Method = c.Method == "POST" ? HttpMethodKind.Post : HttpMethodKind.Get,
            Body = body,
        };
        ScriptChain chain = new(c.Chain is { } steps
            ? [.. steps.Select(s => new ScriptStep(s.Origin, ScriptSet.Empty with { PreRequest = new(s.Language, s.Pre), PostResponse = new(s.Language, s.Post) }))]
            : [new ScriptStep("Users API", ScriptSet.Empty with { PreRequest = new(c.Language, pre ?? c.Pre ?? ""), PostResponse = new(c.Language, post ?? c.Post ?? "") })]);
        VariableScopes scopes = new(new(Guid.NewGuid(), "Test", Pairs(c.Env)), [], Pairs(c.Globals));
        var limits = ScriptLimits.Default with { Timeout = TimeSpan.FromSeconds(c.TimeoutSeconds) };

        ScriptedSendReport report = null!;
        for (var i = 0; i < c.Sends; i++)
        {
            report = await sender.SendAsync(new(request, scopes, chain, true, true) { Limits = limits }, TestContext.Current.CancellationToken);
            scopes = Apply(scopes, report.Changes);
        }

        return (report, transport);
    }

    /// <summary>Runs a case and checks every expectation it declares.</summary>
    /// <param name="c">The case.</param>
    internal static async Task Verify(Case c)
    {
        var (report, transport) = await Run(c);

        var failed = report.Runs.FirstOrDefault(r => !r.Succeeded);
        if (c.Error is null)
        {
            Assert.True(failed is null, failed?.Error?.Message);
        }
        else
        {
            Assert.NotNull(failed);
            Assert.Contains(c.Error, failed.Error!.Message, StringComparison.Ordinal);
            Assert.Equal(c.Aborts, report.Report is null);
            Assert.Equal(!c.Aborts, report.Report?.Result.IsSuccess ?? false);
        }

        if (c.Tests is { } tests)
        {
            Assert.Equal(tests, report.Tests.Select(t => t.Passed ? $"{t.Name}|P" : $"{t.Name}|F|{t.Message}"));
        }

        if (c.Console is { } console)
        {
            Assert.Equal(console, report.Console.Select(e => e.Message));
        }

        if (c.Changes is { } changes)
        {
            Assert.Equal(changes, report.Changes.Select(Label));
        }

        if (c.Aborts)
        {
            Assert.Empty(transport.Requests);
            return;
        }

        var sent = transport.Requests[^1];
        foreach (var header in c.SentHeaders)
        {
            var parts = header.Split('=', 2);
            Assert.Contains(sent.Headers, h => h.Key == parts[0] && h.Value == parts[1]);
        }

        if (c.SentUrl is { } url)
        {
            Assert.Equal(url, sent.Url);
        }
    }
}
