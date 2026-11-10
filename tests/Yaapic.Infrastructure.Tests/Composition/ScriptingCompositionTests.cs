using Yaapic.Composition;
using Yaapic.Core.Scripting;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Scripting;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Tests.EndToEnd;
using Yaapic.Scripting.QuickJs;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using TestResult = Yaapic.Domain.Scripting.TestResult;

namespace Yaapic.Infrastructure.Tests.Composition;

public sealed class ScriptingCompositionTests : IDisposable
{
    private const string Secret = "s3cr3t-value-123";
    private const string Passphrase = "correct horse battery";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "yaapic-" + Guid.NewGuid().ToString("N"));
    private readonly string _vaults = Path.Combine(Path.GetTempPath(), "yaapic-vaults-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        TempWorkspace.Delete(_root);
        TempWorkspace.Delete(_vaults);
    }

    private ShellViewModel Create(params IScriptEngine[] engines) =>
        AppComposition.CreateShell(_root, new FakeDialogService(), null, new TempVaultLocation(_vaults), engines);

    private static FakeScriptEngine Js(Func<ScriptInvocation, ScriptRunResult> reply) => new(reply);

    private static ScriptRunResult Result(ScriptInvocation i, ConsoleEntry[]? console = null, TestResult[]? tests = null) =>
        new(i.Phase, i.Origin, ScriptLanguage.JavaScript, ScriptRunStatus.Succeeded, null, console ?? [], tests ?? [], TimeSpan.Zero);

    private static async Task SetUpVaultAsync(ShellViewModel shell)
    {
        shell.Vault.SelectedProtector = shell.Vault.Protectors.Single(p => p.Id == "passphrase");
        shell.Vault.Passphrase = Passphrase;
        shell.Vault.ConfirmPassphrase = Passphrase;
        await shell.Vault.SetupCommand.ExecuteAsync(null);
        Assert.Equal(VaultState.Unlocked, shell.Vault.State);
    }

    private static async Task SaveSecretEnvironmentAsync(ShellViewModel shell)
    {
        shell.Environments.AddCommand.Execute(null);
        var item = shell.Environments.Environments.Single();
        var row = item.Variables.Rows[0];
        row.Key = "token";
        row.IsSecret = true;
        row.Value = Secret;
        await shell.Environments.SaveCommand.ExecuteAsync(item);
        Assert.Null(shell.Environments.LastError);
    }

    [Fact]
    public async Task ShellCreationWorksWithTheRealEnginesWhetherOrNotTheNativesArePresent()
    {
        var shell = AppComposition.CreateShell(_root, new FakeDialogService());

        await shell.InitializeAsync();

        Assert.Single(shell.Tabs.Tabs);
        Assert.All(shell.ScriptEngineWarnings, w => Assert.Contains("engine unavailable: ", w, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARequestWithScriptsSendsAndFillsTheConsoleAndTheTests()
    {
        using var server = new LoopbackServer();
        var engine = Js(i => Result(
            i,
            [new ConsoleEntry(DateTimeOffset.UnixEpoch, ConsoleLevel.Log, ConsoleSource.Script, "script says hi", i.Origin)],
            [new TestResult("status is ok", true, null, TimeSpan.Zero, i.Origin)]));
        var shell = Create(engine);
        await shell.InitializeAsync();
        var tab = shell.Tabs.ActiveTab!;
        tab.Url = server.BaseUrl + "/echo";
        tab.Scripts.PostResponse.Source = "pm.test('status is ok', () => {});";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Contains(shell.Console.Lines, l => l.Message == "script says hi");
        Assert.Equal(1, tab.Response.Tests.PassedCount);
        Assert.Single(engine.Invocations);
    }

    [Fact]
    [Trait("Category", "RealEngine")]
    public async Task TheRealJavaScriptEngineRunsAScriptAndWritesToTheConsole()
    {
        using var server = new LoopbackServer();
        var shell = Create(new QuickJsEngine());
        await shell.InitializeAsync();
        var unavailable = shell.ScriptEngineWarnings.FirstOrDefault(w => w.StartsWith("JavaScript", StringComparison.Ordinal));
        Assert.SkipWhen(unavailable is not null, "The QuickJS-NG native library is not available: " + unavailable);
        var tab = shell.Tabs.ActiveTab!;
        tab.Url = server.BaseUrl + "/echo";
        tab.Scripts.PostResponse.Source = "console.log('from quickjs'); pm.test('ok', function () { pm.expect(pm.response.code).to.equal(200); });";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Contains(shell.Console.Lines, l => l.Message == "from quickjs");
        Assert.Equal(1, tab.Response.Tests.PassedCount);
    }

    [Fact]
    public async Task ASecretVariableIsAbsentFromTheYamlAndPresentAsCiphertextInTheVaultFile()
    {
        var shell = Create();
        await shell.InitializeAsync();
        await SetUpVaultAsync(shell);

        await SaveSecretEnvironmentAsync(shell);

        var yaml = string.Concat(Directory.EnumerateFiles(_root, "*.yaml", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.Contains("token", yaml, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, yaml, StringComparison.Ordinal);
        var vaultFile = Assert.Single(Directory.EnumerateFiles(_vaults, "*.vault"));
        Assert.DoesNotContain(Secret, await File.ReadAllTextAsync(vaultFile, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.True(new FileInfo(vaultFile).Length > 0);
    }

    [Fact]
    public async Task LockingTheVaultReloadsTheEnvironmentsWithEmptySecretValues()
    {
        var shell = Create();
        await shell.InitializeAsync();
        await SetUpVaultAsync(shell);
        await SaveSecretEnvironmentAsync(shell);

        await shell.Vault.LockCommand.ExecuteAsync(null);

        Assert.Equal("", shell.Environments.Environments.Single().Variables.Rows.Single(r => r.Key == "token").Value);

        shell.Vault.UnlockPassphrase = Passphrase;
        await shell.Vault.UnlockCommand.ExecuteAsync(null);

        Assert.Equal(Secret, shell.Environments.Environments.Single().Variables.Rows.Single(r => r.Key == "token").Value);
    }

    [Fact]
    public async Task SendingWithALockedGlobalSecretWarnsAndSendsItEmpty()
    {
        using var server = new LoopbackServer();
        var shell = Create(Js(i => Result(i)));
        await shell.InitializeAsync();
        await SetUpVaultAsync(shell);
        var row = shell.Globals.Variables.Rows[0];
        row.Key = "token";
        row.Value = Secret;
        row.IsSecret = true;
        await shell.Globals.SaveCommand.ExecuteAsync(null);
        Assert.Null(shell.Globals.LastError);
        await shell.Vault.LockCommand.ExecuteAsync(null);
        var tab = shell.Tabs.ActiveTab!;
        tab.Url = server.BaseUrl + "/echo?x={{token}}";

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Contains(shell.Globals.Variables.Rows, r => r is { Key: "token", IsSecret: true });
        Assert.Equal(RequestEditorViewModel.VaultLockedWarning, tab.Response.UnresolvedWarning);
    }

    [Fact]
    public async Task ConsoleToggleAndClearWorkOnAComposedShell()
    {
        var shell = Create();
        await shell.InitializeAsync();
        shell.Console.AppendSystem(ConsoleLevel.Info, "hello");
        Assert.False(shell.Console.IsEmpty);

        shell.ToggleConsoleCommand.Execute(null);
        Assert.True(shell.IsConsoleOpen);
        shell.ClearConsoleCommand.Execute(null);

        Assert.True(shell.Console.IsEmpty);
        Assert.True(shell.IsConsoleOpen);
    }

    [Fact]
    public async Task PaletteVaultCommandsFollowTheVaultState()
    {
        var shell = Create();
        await shell.InitializeAsync();
        Assert.Equal(["Set up the secret vault..."], VaultTitles(shell));

        await SetUpVaultAsync(shell);
        Assert.Equal(["Lock the vault"], VaultTitles(shell));

        await shell.Vault.LockCommand.ExecuteAsync(null);
        Assert.Equal(["Unlock the vault..."], VaultTitles(shell));
    }

    [Fact]
    public async Task AnUnavailableEngineProducesAWarning()
    {
        var js = Js(_ => throw new NotSupportedException());
        js.UnavailableReason = "native library missing";
        var lua = Js(_ => throw new NotSupportedException());
        lua.Language = ScriptLanguage.Lua;
        var shell = Create(js, lua);

        await shell.InitializeAsync();

        Assert.Equal(["JavaScript engine unavailable: native library missing"], shell.ScriptEngineWarnings);
        Assert.True(shell.HasScriptEngineWarnings);
    }

    private static string[] VaultTitles(ShellViewModel shell)
    {
        shell.Palette.OpenCommand.Execute(null);
        return [.. shell.Palette.Results.Select(r => r.Item.Title).Where(t => t.Contains("vault", StringComparison.Ordinal))];
    }

    private sealed class TempVaultLocation(string directory) : IVaultLocation
    {
        public string GetVaultPath(string workspaceId)
        {
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, workspaceId + ".vault");
        }
    }
}
