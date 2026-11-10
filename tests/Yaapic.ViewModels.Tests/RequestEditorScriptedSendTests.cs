using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorScriptedSendTests
{
    private static readonly HttpResponseData Ok = new(200, "OK", [], [], TimeSpan.FromMilliseconds(5), "https://x.test");
    private static readonly ScriptBlock Code = new(ScriptLanguage.JavaScript, "x();");

    private readonly FakeScriptVariableApplier _applier = new();
    private readonly ConsoleViewModel _console = new();
    private readonly List<RequestSentEventArgs> _sent = [];
    private FakeHttpTransport _transport = new((_, _) => Task.FromResult(SendResult.Success(Ok)));

    private static ScriptRunResult Succeeded(ScriptInvocation i, params TestResult[] tests) =>
        new(i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Succeeded, null, [], tests, TimeSpan.Zero);

    private static ScriptRunResult Failed(ScriptInvocation i) =>
        new(i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Failed, new ScriptError("boom"), [], [], TimeSpan.Zero);

    private RequestEditorViewModel Editor(FakeScriptEngine engine, bool post = false, bool secretGlobal = false)
    {
        ScriptEngineRegistry registry = new();
        registry.Add(engine);
        var sender = new RequestSender(_transport);
        var scopes = new FakeVariableScopeProvider { Scopes = new VariableScopes(new EnvironmentDefinition(Guid.NewGuid(), "dev", []), [], secretGlobal ? [new("token", "", true, true)] : []) };
        var services = new ScriptSendServices(
            new ScriptedRequestSender(sender, registry, TimeProvider.System),
            new FakeScriptChainProvider(),
            _console,
            _applier);
        var editor = RequestEditorViewModel.CreateNew(sender, scopes, scripting: services);
        editor.Url = "https://x.test";
        editor.Scripts.PreRequest.Source = Code.Source;
        if (post)
        {
            editor.Scripts.PostResponse.Source = Code.Source;
        }

        editor.Sent += (_, e) => _sent.Add(e);
        return editor;
    }

    [Fact]
    public async Task PreRequestHeaderReachesTheTransportAndSentCarriesTheMutatedDefinition()
    {
        var editor = Editor(new FakeScriptEngine(i =>
        {
            i.Host.Request.Headers.Add(new KeyValueEntry("X-Script", "1"));
            return Succeeded(i) with { Console = [new ConsoleEntry(DateTimeOffset.Now, ConsoleLevel.Log, ConsoleSource.Script, "hello")] };
        }));

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Contains(_console.Lines, l => l.Message == "hello");
        Assert.Contains(_transport.Requests.Single().Headers, h => h.Key == "X-Script");
        var sent = Assert.Single(_sent);
        Assert.Contains(sent.Request.Headers, h => h.Key == "X-Script");
        Assert.Equal(ResponseState.Completed, editor.Response.State);
        Assert.Contains(_console.Lines, l => l.Kind == ConsoleLineKind.Header);
        Assert.Contains(_console.Lines, l => l.Kind == ConsoleLineKind.Network);
    }

    [Fact]
    public async Task ScriptedSendWarnsWhenAGlobalSecretIsEmptyBecauseTheVaultIsLocked()
    {
        var editor = Editor(new FakeScriptEngine(i => Succeeded(i)), secretGlobal: true);
        editor.Url = "https://x.test/{{token}}";

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal(RequestEditorViewModel.VaultLockedWarning, editor.Response.UnresolvedWarning);
        Assert.Equal("https://x.test/", _transport.Requests.Single().Url);
    }

    [Fact]
    public async Task PostResponseTestsLandInTheResponsePanelAndTheConsole()
    {
        var test = new TestResult("status is 200", true, null, TimeSpan.Zero, "request");
        var editor = Editor(new FakeScriptEngine(i => i.Phase == ScriptPhase.PostResponse ? Succeeded(i, test) : Succeeded(i)), post: true);

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal(1, editor.Response.Tests.PassedCount);
        Assert.Contains(_console.Lines, l => l.Kind == ConsoleLineKind.TestPassed);
        Assert.Single(_sent);
    }

    [Fact]
    public async Task AbortedPreRequestRaisesNoSentAndShowsTheErrorState()
    {
        var editor = Editor(new FakeScriptEngine(Failed));

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Empty(_sent);
        Assert.Empty(_transport.Requests);
        Assert.Equal(ResponseState.Failed, editor.Response.State);
        Assert.Contains("boom", editor.Response.ErrorMessage);
        Assert.Contains(_console.Lines, l => l.Kind == ConsoleLineKind.Error);
    }

    [Fact]
    public async Task PostResponseErrorKeepsTheResponse()
    {
        var editor = Editor(new FakeScriptEngine(i => i.Phase == ScriptPhase.PostResponse ? Failed(i) : Succeeded(i)), post: true);

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Completed, editor.Response.State);
        Assert.Single(_sent);
    }

    [Fact]
    public async Task VariableChangesAreHandedToTheApplier()
    {
        var editor = Editor(new FakeScriptEngine(i =>
        {
            i.Host.SetVariable(ScriptScope.Environment, "token", "abc");
            return Succeeded(i);
        }));

        await editor.SendCommand.ExecuteAsync(null);

        var (id, changes) = Assert.Single(_applier.Calls);
        Assert.Equal(editor.Id, id);
        Assert.Equal(new VariableChange(VariableScopeKind.Environment, "token", "abc"), Assert.Single(changes));
    }

    [Fact]
    public async Task ApplierErrorsBecomeConsoleWarnings()
    {
        _applier.Errors = ["disk full"];
        var editor = Editor(new FakeScriptEngine(i =>
        {
            i.Host.SetVariable(ScriptScope.Environment, "token", "abc");
            return Succeeded(i);
        }));

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Contains(_console.Lines, l => l.Kind == ConsoleLineKind.Warn && l.Message == "Could not save variable changes: disk full");
        Assert.Equal(1, _console.WarnCount);
    }

    [Fact]
    public async Task CancelDuringASlowScriptEndsWithRequestCanceled()
    {
        var engine = new FakeScriptEngine(i => Succeeded(i)) { Delay = TimeSpan.FromSeconds(30) };
        var editor = Editor(engine);

        var send = editor.SendCommand.ExecuteAsync(null);
        editor.CancelCommand.Execute(null);
        await send;

        Assert.Equal(ResponseState.Failed, editor.Response.State);
        Assert.Equal("Request canceled", editor.Response.ErrorMessage);
        Assert.Empty(_sent);
        Assert.False(editor.IsSending);
    }

    [Fact]
    public async Task IsSendingCoversTheWholePipeline()
    {
        var gate = new TaskCompletionSource<SendResult>();
        _transport = new FakeHttpTransport((_, _) => gate.Task);
        var wasSendingInPostResponse = false;
        RequestEditorViewModel? editor = null;
        editor = Editor(
            new FakeScriptEngine(i =>
            {
                wasSendingInPostResponse |= i.Phase == ScriptPhase.PostResponse && editor!.IsSending;
                return Succeeded(i);
            }),
            post: true);

        var send = editor.SendCommand.ExecuteAsync(null);

        Assert.True(editor.IsSending);
        Assert.Equal(ResponseState.Sending, editor.Response.State);

        gate.SetResult(SendResult.Success(Ok));
        await send;

        Assert.True(wasSendingInPostResponse);
        Assert.False(editor.IsSending);
    }
}
