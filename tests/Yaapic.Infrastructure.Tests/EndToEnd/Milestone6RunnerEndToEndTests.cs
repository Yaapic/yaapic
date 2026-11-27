using System.Text;
using System.Xml;
using Yaapic.Core.Import;
using Yaapic.Core.Runner;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.ViewModels.Responses;
using Yaapic.ViewModels.Runner;
using Yaapic.ViewModels.Shell;
using static Yaapic.Infrastructure.Tests.EndToEnd.ScriptedShellFixture;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// The collection runner end to end: loopback server, shell view model, real QuickJS and Lua engines, real files and stores.
/// The natives come from <c>native/Build-Natives.ps1</c>; a missing library fails the tests instead of skipping them.
/// </summary>
public sealed class Milestone6RunnerEndToEndTests : IDisposable
{
    private const string Lua = "lua";
    private const string Secret = "sk-run-Secret-4711";

    private readonly LoopbackServer _server = new();
    private readonly ScriptedShellFixture _fx = new();

    public void Dispose()
    {
        _server.Dispose();
        _fx.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Url(string path) => _server.BaseUrl + path;

    private static RunnerViewModel OpenRunner(ShellViewModel shell)
    {
        var runner = shell.Runner!;
        runner.Open(shell.Collections.Roots.First(r => r.IsFolder).Node);
        return runner;
    }

    private static async Task<RunnerViewModel> RunAsync(ShellViewModel shell, Action<RunnerSetupViewModel>? setup = null)
    {
        var runner = OpenRunner(shell);
        setup?.Invoke(runner.Setup);
        await runner.RunCommand.ExecuteAsync(null);
        return runner;
    }

    private static string[] Names(RunnerViewModel runner) => [.. runner.Rows.Select(r => r.Name)];

    private static string[] ConsoleOf(RunnerResultRowViewModel row) => [.. row.Result.Console.Where(c => c.Source == ConsoleSource.Script).Select(c => c.Message)];

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(50, Ct);
        }

        Assert.True(condition(), "The condition was not met in time.");
    }

    [Fact]
    public async Task ThreeRequestsTwoIterationsGiveSixOrderedRows()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/status/200"));
        _fx.WriteRequest("run", "R2", Url("/status/201"));
        _fx.WriteRequest("run", "R3", Url("/status/404"));
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell, s => s.Iterations = 2);

        Assert.Equal(RunStatus.Completed, runner.Result!.Status);
        Assert.Equal(["R1", "R2", "R3", "R1", "R2", "R3"], Names(runner));
        Assert.Equal([200, 201, 404, 200, 201, 404], runner.Rows.Select(r => r.Result.StatusCode!.Value));
        Assert.Equal([1, 1, 1, 2, 2, 2], runner.Rows.Select(r => r.Iteration));
        Assert.Equal(6, runner.SummarySent);
        Assert.Equal(0, runner.SummaryErrors);
        Assert.Equal(6, _server.Hits.Count);
    }

    [Fact]
    public async Task ACsvFileDrivesTheUrlAndTheScriptsOfBothEngines()
    {
        const string js = "yaapic.test('user', () => yaapic.expect(yaapic.iterationData.get('user')).to.equal(yaapic.variables.get('user')));\n"
            + "console.log('js:' + yaapic.iterationData.get('user') + '|' + yaapic.iterationData.get('note'));";
        const string lua = "yaapic.test('user', function() yaapic.expect(yaapic.iterationData.get('user')).to.equal(yaapic.variables.get('user')) end)\n"
            + "console.log('lua:' .. yaapic.iterationData.get('user') .. '|' .. yaapic.iterationData.get('note'))";
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo?u={{user}}"), post: js);
        _fx.WriteRequest("run", "R2", Url("/echo?l={{user}}"), post: lua, postLanguage: Lua);
        var csv = Path.Combine(_fx.Root, "users.csv");
        await File.WriteAllTextAsync(csv, "user,note\nalice,\"hello, world\"\nbob,plain\ncarol,last\n", Ct);
        _fx.Dialogs.PickFileAnswer = csv;
        var shell = await _fx.NewShellAsync(withLua: true);
        var runner = OpenRunner(shell);

        await runner.Setup.ChooseDataFileCommand.ExecuteAsync(null);
        Assert.Equal(3, runner.Setup.DataRowCount);
        Assert.Equal(3, runner.Setup.Iterations);
        await runner.RunCommand.ExecuteAsync(null);

        Assert.Equal(6, runner.Rows.Count);
        Assert.All(runner.Rows, r => Assert.Equal(RunRequestOutcome.Passed, r.Outcome));
        Assert.All(runner.Rows, r => Assert.Single(r.Result.Tests));
        Assert.Equal(["js:alice|hello, world"], ConsoleOf(runner.Rows[0]));
        Assert.Equal(["lua:alice|hello, world"], ConsoleOf(runner.Rows[1]));
        Assert.Equal(["js:bob|plain"], ConsoleOf(runner.Rows[2]));
        Assert.Equal(["lua:carol|last"], ConsoleOf(runner.Rows[5]));
        Assert.Contains("/echo?u=alice", _server.Hits);
        Assert.Contains("/echo?l=carol", _server.Hits);
    }

    [Fact]
    public async Task AScriptSeesTheIterationIndexAndCount()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo"), post: "console.log(yaapic.info.iteration + '/' + yaapic.info.iterationCount);");
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell, s => s.Iterations = 3);

        Assert.Equal(["0/3", "1/3", "2/3"], runner.Rows.SelectMany(ConsoleOf));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AVariableSetByOneRequestReachesTheNextAndIsPersistedOnlyWhenAsked(bool keep)
    {
        _fx.WriteEnvironment("Dev");
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo"), post: "yaapic.environment.set('token', 'tok-4242');");
        _fx.WriteRequest("run", "R2", Url("/headers"), headers: ("X-Token", "{{token}}"), post: "console.log('got:' + yaapic.response.json()['X-Token']);");
        var shell = await _fx.NewShellAsync();
        shell.Environments.ActiveEnvironment = shell.Environments.Environments.Single(e => e.Name == "Dev");
        var file = _fx.PathOf("environments", "dev.env.yaml");
        var before = await File.ReadAllTextAsync(file, Ct);

        var runner = await RunAsync(shell, s => s.PersistVariables = keep);

        Assert.Equal(["got:tok-4242"], ConsoleOf(runner.Rows[1]));
        var after = await File.ReadAllTextAsync(file, Ct);
        if (keep)
        {
            Assert.Contains("tok-4242", after, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(before, after);
        }
    }

    [Fact]
    public async Task SetNextRequestJumpsForward()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo"), post: "yaapic.execution.setNextRequest('R3');");
        _fx.WriteRequest("run", "R2", Url("/echo"));
        _fx.WriteRequest("run", "R3", Url("/echo"));
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell);

        Assert.Equal(RunStatus.Completed, runner.Result!.Status);
        Assert.Equal(["R1", "R3"], Names(runner));
    }

    [Fact]
    public async Task SetNextRequestNullStopsTheRun()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo"), post: "yaapic.execution.setNextRequest(null);");
        _fx.WriteRequest("run", "R2", Url("/echo"));
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell);

        Assert.Equal(RunStatus.Stopped, runner.Result!.Status);
        Assert.Equal(["R1"], Names(runner));
    }

    [Fact]
    public async Task SetNextRequestToAnUnknownNameFailsTheRun()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo"), post: "yaapic.execution.setNextRequest('Nope');");
        _fx.WriteRequest("run", "R2", Url("/echo"));
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell);

        Assert.Equal(RunStatus.Failed, runner.Result!.Status);
        Assert.Contains("Nope", runner.Result.Error, StringComparison.Ordinal);
        Assert.Equal(["R1"], Names(runner));
    }

    [Fact]
    public async Task ASetNextRequestLoopHitsTheGuard()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/status/200"), post: "yaapic.execution.setNextRequest('R1');");
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell);

        Assert.Equal(RunStatus.Failed, runner.Result!.Status);
        Assert.Equal(Strings.RunnerLoopGuard, runner.Result.Error);
        Assert.Equal(1000, runner.Rows.Count);
    }

    [Fact]
    public async Task SkipRequestInAFolderScriptSkipsOneRequestWithoutSendingIt()
    {
        _fx.WriteFolder("run", "Run", pre: "if (yaapic.request.url.toString().includes('/skipme')) { yaapic.execution.skipRequest(); }");
        _fx.WriteRequest("run", "R1", Url("/one"));
        _fx.WriteRequest("run", "R2", Url("/skipme"));
        _fx.WriteRequest("run", "R3", Url("/three"));
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell);

        Assert.Equal([RunRequestOutcome.Passed, RunRequestOutcome.Skipped, RunRequestOutcome.Passed], runner.Rows.Select(r => r.Outcome));
        Assert.Equal(1, runner.SummarySkipped);
        Assert.Equal(["/one", "/three"], _server.Hits);
    }

    [Fact]
    public async Task StopOnFailureEndsTheRunAfterAFailedTest()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo"));
        _fx.WriteRequest("run", "R2", Url("/echo"), post: "yaapic.test('bad', () => yaapic.expect(1).to.equal(2));");
        _fx.WriteRequest("run", "R3", Url("/echo"));
        var shell = await _fx.NewShellAsync();

        var runner = await RunAsync(shell, s => s.StopOnFailure = true);

        Assert.Equal(RunStatus.StoppedOnFailure, runner.Result!.Status);
        Assert.Equal(["R1", "R2"], Names(runner));
        Assert.Equal(RunRequestOutcome.Failed, runner.Rows[1].Outcome);
    }

    [Fact]
    public async Task StopCancelsARequestInFlight()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/delay/2000"));
        var shell = await _fx.NewShellAsync();
        var runner = OpenRunner(shell);

        var run = runner.RunCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => _server.Hits.Contains("/delay/2000"));
        runner.StopCommand.Execute(null);
        await run;

        Assert.Equal(RunStatus.Canceled, runner.Result!.Status);
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public async Task TheReportsParseAndNeverContainAResolvedSecret()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/headers"), headers: ("X-Key", "{{apiKey}}"), post: "console.log('key=' + yaapic.variables.get('apiKey')); yaapic.test('ok', () => {});");
        var shell = await _fx.NewShellAsync(protectors: new PassphraseKeyProtectorFactory(100_000));
        await SetUpVaultAsync(shell);
        await AddSecretEnvironmentAsync(shell);
        _fx.Dialogs.SaveFileAnswer = true;

        var runner = await RunAsync(shell);
        await runner.ExportJsonCommand.ExecuteAsync(null);
        await runner.ExportJUnitCommand.ExecuteAsync(null);

        var saved = _fx.Dialogs.SavedFiles;
        Assert.Equal(2, saved.Count);
        var json = Encoding.UTF8.GetString(saved[0].Content);
        var xml = Encoding.UTF8.GetString(saved[1].Content);
        Assert.NotNull(DataNodeJson.Parse(json));
        using (var reader = XmlReader.Create(new StringReader(xml)))
        {
            while (reader.Read())
            {
            }
        }

        Assert.DoesNotContain(Secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, xml, StringComparison.Ordinal);
        Assert.Contains("R1", json, StringComparison.Ordinal);
        Assert.Contains("R1", xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunnerSendsDoNotReachTheHistory()
    {
        _fx.WriteFolder("run", "Run");
        _fx.WriteRequest("run", "R1", Url("/echo"));
        var other = _fx.WriteRequest("", "Editor", Url("/echo"));
        var shell = await _fx.NewShellAsync();

        await RunAsync(shell);
        await shell.History.RefreshAsync();

        Assert.Empty(shell.History.Groups);

        // Control: the history does record an editor send.
        await Open(shell, other).SendCommand.ExecuteAsync(null);
        await WaitUntilAsync(() =>
        {
            shell.History.RefreshAsync().GetAwaiter().GetResult();
            return shell.History.Groups.Count > 0;
        });
    }

    [Fact]
    public async Task SkipRequestInTheEditorShowsAMessageAndSendsNothing()
    {
        var id = _fx.WriteRequest("", "Skip me", Url("/echo"), pre: "yaapic.execution.skipRequest();");
        var shell = await _fx.NewShellAsync();
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ResponseState.Failed, tab.Response.State);
        Assert.Equal(Strings.ResponseSkippedByScript, tab.Response.ErrorMessage);
        Assert.Empty(_server.Hits);
    }

    private static string ConsoleOfAll(LoopbackServer server) => string.Join('\n', server.Hits);

    private static async Task SetUpVaultAsync(ShellViewModel shell)
    {
        var vault = shell.Vault;
        vault.SelectedProtector = vault.Protectors.Single(p => p.Id == "passphrase");
        vault.Passphrase = "correct horse battery";
        vault.ConfirmPassphrase = "correct horse battery";
        await vault.SetupCommand.ExecuteAsync(null);
        Assert.Equal(VaultState.Unlocked, vault.State);
    }

    private static async Task AddSecretEnvironmentAsync(ShellViewModel shell)
    {
        shell.Environments.AddCommand.Execute(null);
        var env = shell.Environments.EditedEnvironment!;
        env.Name = "Prod";
        var row = env.Variables.Rows[^1];
        row.Key = "apiKey";
        row.Value = Secret;
        row.ToggleSecretCommand.Execute(null);
        Assert.True(row.IsSecret);
        await shell.Environments.SaveCommand.ExecuteAsync(env);
        shell.Environments.ActiveEnvironment = env;
    }
}
