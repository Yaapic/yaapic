using System.Text;
using System.Text.Json.Nodes;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using static Yaapic.Infrastructure.Tests.EndToEnd.ScriptedShellFixture;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// The secret vault end to end: loopback server, shell view model, real QuickJS engine, real files, a passphrase vault in a
/// temporary directory (never the user's real vault directory) with a lowered iteration count.
/// </summary>
public sealed class Milestone3VaultEndToEndTests : IClassFixture<LoopbackServer>, IDisposable
{
    private const string Passphrase = "correct horse battery";
    private const string ApiKey = "sk-live-ApiKey-4711";
    private const string GlobalSecret = "glob-SECRET-9001";
    private const string CollectionSecret = "coll-SECRET-1337";

    private readonly LoopbackServer _server;
    private readonly ScriptedShellFixture _fx = new();

    public Milestone3VaultEndToEndTests(LoopbackServer server) => _server = server;

    public void Dispose() => _fx.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Url(string path) => _server.BaseUrl + path;

    private static PassphraseKeyProtectorFactory Passphrases() => new(100_000);

    private Task<ShellViewModel> NewShellAsync(IKeyProtectorFactory? protectors = null) =>
        _fx.NewShellAsync(protectors: protectors ?? Passphrases());

    private static async Task SetUpVaultAsync(ShellViewModel shell, string protectorId = "passphrase")
    {
        var vault = shell.Vault;
        var choice = vault.Protectors.Single(p => p.Id == protectorId);
        vault.SelectedProtector = choice;
        if (choice.RequiresPassphrase)
        {
            vault.Passphrase = Passphrase;
            vault.ConfirmPassphrase = Passphrase;
        }

        await vault.SetupCommand.ExecuteAsync(null);
        Assert.Equal(VaultState.Unlocked, vault.State);
    }

    private static async Task UnlockAsync(ShellViewModel shell, string passphrase = Passphrase)
    {
        shell.Vault.UnlockPassphrase = passphrase;
        await shell.Vault.UnlockCommand.ExecuteAsync(null);
    }

    private static void AddRow(KeyValueEditorViewModel editor, string key, string value, bool secret)
    {
        var row = editor.Rows[^1];
        row.Key = key;
        row.Value = value;
        if (secret)
        {
            row.ToggleSecretCommand.Execute(null);
            Assert.True(row.IsSecret);
        }
    }

    /// <summary>Creates a secret in an environment, in the globals and in the collection holding <paramref name="collectionRequest"/>.</summary>
    private static async Task<EnvironmentItemViewModel> SeedAsync(ShellViewModel shell, Guid collectionRequest)
    {
        shell.Environments.AddCommand.Execute(null);
        var env = shell.Environments.EditedEnvironment!;
        env.Name = "Prod";
        AddRow(env.Variables, "apiKey", ApiKey, true);
        AddRow(env.Variables, "region", "eu-west", false);
        await shell.Environments.SaveCommand.ExecuteAsync(env);
        shell.Environments.ActiveEnvironment = env;

        AddRow(shell.Globals.Variables, "globalKey", GlobalSecret, true);
        await shell.Globals.SaveCommand.ExecuteAsync(null);

        Assert.True(shell.Collections.OpenCollectionSettings(collectionRequest));
        var settings = shell.Collections.SettingsEditor!;
        AddRow(settings.Variables, "collKey", CollectionSecret, true);
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Null(settings.LastError);
        await WaitUntilAsync(() => shell.Collections.FindCollectionVariables(collectionRequest).Any(v => v.Key == "collKey" && v.Value == CollectionSecret));
        return env;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(50, Ct);
        }

        Assert.True(condition(), "The condition was not met in time.");
    }

    private static byte[] ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string ReadText(string path) => Encoding.UTF8.GetString(ReadShared(path));

    private JsonArray VaultEntries() => (JsonArray)JsonNode.Parse(ReadText(_fx.VaultFile))!["entries"]!;

    private string EnvironmentFile() => Directory.EnumerateFiles(_fx.PathOf("environments"), "*.env.yaml").Single();

    /// <summary>Opens the vault file independently of the shell, as another process would after a restart.</summary>
    private async Task<FileSecretVault> OpenVaultAsync()
    {
        var vault = new FileSecretVault(_fx.Root, new ScriptedShellFixture.TempVaultLocation(Path.GetDirectoryName(_fx.VaultFile)!), Passphrases());
        await vault.InitializeAsync(Ct);
        Assert.True(await vault.UnlockAsync(Passphrase, Ct));
        return vault;
    }

    private Guid WriteCollection()
    {
        _fx.WriteFolder("acme", "Acme");
        return _fx.WriteRequest("acme", "Inner", Url("/echo"));
    }

    [Fact]
    public async Task SecretsNeverReachTheWorkspaceFilesAndTheVaultFileHoldsCiphertext()
    {
        var inner = WriteCollection();
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);

        await SeedAsync(shell, inner);

        var vaultFile = _fx.VaultFile;
        var files = Directory.EnumerateFiles(_fx.Root, "*", SearchOption.AllDirectories).Append(vaultFile).ToList();
        Assert.Contains(files, f => f.EndsWith("globals.yaml", StringComparison.Ordinal));
        foreach (var secret in new[] { ApiKey, GlobalSecret, CollectionSecret })
        {
            var needle = Encoding.UTF8.GetBytes(secret);
            foreach (var file in files)
            {
                Assert.True(ReadShared(file).AsSpan().IndexOf(needle) < 0, $"{file} contains a secret value.");
            }
        }

        var environment = ReadText(EnvironmentFile());
        Assert.Contains("secret: true", environment, StringComparison.Ordinal);
        Assert.Contains("value: \"\"", environment, StringComparison.Ordinal);
        Assert.Contains("eu-west", environment, StringComparison.Ordinal);
        Assert.Contains("secret: true", ReadText(_fx.PathOf("globals.yaml")), StringComparison.Ordinal);
        Assert.Contains("secret: true", ReadText(_fx.PathOf("requests", "acme", "folder.yaml")), StringComparison.Ordinal);

        var entries = VaultEntries();
        Assert.Equal(["Collection", "Environment", "Globals"], entries.Select(e => (string)e!["kind"]!).Order());
        Assert.All(entries, e => Assert.NotEmpty((string)e!["ciphertext"]!));
    }

    [Fact]
    public async Task SecretResolvesWhenSendingAndHistoryKeepsTheTemplate()
    {
        var inner = WriteCollection();
        var id = _fx.WriteRequest("", "Keyed", Url("/echo"), headers: ("X-Api-Key", "{{apiKey}}"));
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);
        await SeedAsync(shell, inner);
        var tab = Open(shell, id);

        await tab.SendCommand.ExecuteAsync(null);

        Assert.Equal(ApiKey, Json(tab).GetProperty("headers").GetProperty("X-Api-Key").GetString());
        var history = _fx.PathOf(".yaapic", "history.jsonl");
        await WaitUntilAsync(() => File.Exists(history) && ReadText(history).Contains("apiKey", StringComparison.Ordinal));
        var text = ReadText(history);
        Assert.Contains("{{apiKey}}", text, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScriptsReadAndWriteSecretsAndEveryOutputIsRedacted()
    {
        var inner = WriteCollection();
        var read = _fx.WriteRequest(
            "",
            "Read",
            Url("/echo"),
            post: "console.log(yaapic.environment.get('apiKey'));\nyaapic.test('leaks ' + 'it', () => { throw new Error('bad ' + yaapic.environment.get('apiKey')); });");
        var error = _fx.WriteRequest("", "Error", Url("/echo"), post: "throw new Error('oops ' + yaapic.environment.get('apiKey'));");
        var write = _fx.WriteRequest("", "Write", Url("/echo"), post: "yaapic.environment.set('apiKey', 'new-secret-9');");
        var pre = _fx.WriteRequest("", "Pre", Url("/echo"), pre: "yaapic.request.headers.add({ key: 'X-From-Script', value: yaapic.environment.get('apiKey') });");
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);
        var env = await SeedAsync(shell, inner);

        var preTab = Open(shell, pre);
        await preTab.SendCommand.ExecuteAsync(null);
        Assert.Equal(ApiKey, Json(preTab).GetProperty("headers").GetProperty("X-From-Script").GetString());

        var readTab = Open(shell, read);
        await readTab.SendCommand.ExecuteAsync(null);
        var logged = Messages(shell, ConsoleLineKind.Log);
        Assert.Contains("••••", logged);
        Assert.DoesNotContain(logged, m => m.Contains(ApiKey, StringComparison.Ordinal));
        var failed = Assert.Single(readTab.Response.Tests.Items, t => !t.Passed);
        Assert.Contains("••••", failed.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, failed.Message, StringComparison.Ordinal);

        var errorTab = Open(shell, error);
        await errorTab.SendCommand.ExecuteAsync(null);
        var scriptError = Assert.Single(errorTab.Response.Tests.Errors);
        Assert.Contains("••••", scriptError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, scriptError.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(shell.Console.Lines, l => l.Message.Contains(ApiKey, StringComparison.Ordinal));

        var writeTab = Open(shell, write);
        await writeTab.SendCommand.ExecuteAsync(null);

        var row = env.Variables.Rows.Single(r => r.Key == "apiKey");
        Assert.True(row.IsSecret);
        Assert.Equal("new-secret-9", row.Value);
        Assert.DoesNotContain("new-secret-9", ReadText(EnvironmentFile()), StringComparison.Ordinal);
        Assert.Contains("secret: true", ReadText(EnvironmentFile()), StringComparison.Ordinal);
        var vault = await OpenVaultAsync();
        Assert.True(vault.TryGet(new SecretRef(SecretOwnerKind.Environment, env.Id, "apiKey"), out var stored));
        Assert.Equal("new-secret-9", stored);
    }

    [Fact]
    public async Task LockedVaultLeavesSecretsUnresolvedKeepsTheEntriesAndRefusesNewSecrets()
    {
        var inner = WriteCollection();
        var id = _fx.WriteRequest("", "Keyed", Url("/echo"), headers: ("X-Api-Key", "{{apiKey}}"));
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);
        var env = await SeedAsync(shell, inner);

        await shell.Vault.LockCommand.ExecuteAsync(null);

        Assert.Equal(VaultState.Locked, shell.Vault.State);
        var locked = shell.Environments.Environments.Single();
        var lockedRow = locked.Variables.Rows.Single(r => r.Key == "apiKey");
        Assert.True(lockedRow.IsSecret);
        Assert.Equal("", lockedRow.Value);
        Assert.Equal("", shell.Globals.Variables.Rows.Single(r => r.Key == "globalKey").Value);
        var tab = Open(shell, id);
        await tab.SendCommand.ExecuteAsync(null);
        Assert.Contains(RequestEditorViewModel.VaultLockedWarning, tab.Response.UnresolvedWarning, StringComparison.Ordinal);
        Assert.DoesNotContain(ApiKey, tab.Response.BodyText, StringComparison.Ordinal);

        var entriesBefore = VaultEntries().Count;
        AddRow(locked.Variables, "plain", "p-1", false);
        await shell.Environments.SaveCommand.ExecuteAsync(locked);
        Assert.Null(shell.Environments.LastError);
        Assert.Equal(entriesBefore, VaultEntries().Count);

        var fresh = locked.Variables.Rows[^1];
        fresh.Key = "another";
        fresh.ToggleSecretCommand.Execute(null);
        Assert.False(fresh.IsSecret);
        Assert.Equal(KeyValueRowViewModel.VaultLockedError, fresh.LastError);

        await UnlockAsync(shell);

        var back = shell.Environments.Environments.Single(e => e.Id == env.Id);
        Assert.Equal(ApiKey, back.Variables.Rows.Single(r => r.Key == "apiKey").Value);
        Assert.Contains(back.Variables.Rows, r => r.Key == "plain" && r.Value == "p-1");
    }

    [Fact]
    public async Task WrongPassphraseIsReportedAndTheRightOneReloadsEverything()
    {
        var inner = WriteCollection();
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);
        await SeedAsync(shell, inner);
        await shell.Vault.LockCommand.ExecuteAsync(null);

        await UnlockAsync(shell, "not the passphrase");

        Assert.Equal(VaultState.Locked, shell.Vault.State);
        Assert.Equal("Wrong passphrase", shell.Vault.LastError);

        await UnlockAsync(shell);

        Assert.Equal(VaultState.Unlocked, shell.Vault.State);
        Assert.Equal(ApiKey, shell.Environments.Environments.Single().Variables.Rows.Single(r => r.Key == "apiKey").Value);
        Assert.Equal(GlobalSecret, shell.Globals.Variables.Rows.Single(r => r.Key == "globalKey").Value);
        await WaitUntilAsync(() => shell.Collections.FindCollectionVariables(inner).Any(v => v.Key == "collKey" && v.Value == CollectionSecret));
    }

    [Fact]
    public async Task RestartedShellStartsLockedWithAPassphraseAndUnlockedWithAnOsStyleProtector()
    {
        var inner = WriteCollection();
        var first = await NewShellAsync();
        await SetUpVaultAsync(first);
        await SeedAsync(first, inner);

        var restarted = await NewShellAsync();

        Assert.Equal(VaultState.Locked, restarted.Vault.State);
        Assert.Equal("", restarted.Environments.Environments.Single().Variables.Rows.Single(r => r.Key == "apiKey").Value);
        await UnlockAsync(restarted);
        Assert.Equal(ApiKey, restarted.Environments.Environments.Single().Variables.Rows.Single(r => r.Key == "apiKey").Value);

        // A second workspace with a protector that needs no passphrase (DPAPI-like).
        using var other = new ScriptedShellFixture();
        var shell = await other.NewShellAsync(protectors: new FakeKeyProtectorFactory(new FakeKeyProtector("fake")));
        await SetUpVaultAsync(shell, "fake");
        AddRow(shell.Globals.Variables, "g", "auto-secret-1", true);
        await shell.Globals.SaveCommand.ExecuteAsync(null);

        var again = await other.NewShellAsync(protectors: new FakeKeyProtectorFactory(new FakeKeyProtector("fake")));

        Assert.Equal(VaultState.Unlocked, again.Vault.State);
        Assert.Equal("auto-secret-1", again.Globals.Variables.Rows.Single(r => r.Key == "g").Value);
    }

    [Fact]
    public async Task TamperedEntryIsLostAloneAndACorruptFileIsLeftUntouched()
    {
        var inner = WriteCollection();
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);
        var env = await SeedAsync(shell, inner);
        var json = JsonNode.Parse(ReadText(_fx.VaultFile))!;
        var victim = json["entries"]!.AsArray().Single(e => (string)e!["kind"]! == "Environment")!;
        var bytes = Convert.FromBase64String((string)victim["ciphertext"]!);
        bytes[0] ^= 0xFF;
        victim["ciphertext"] = Convert.ToBase64String(bytes);
        File.WriteAllText(_fx.VaultFile, json.ToJsonString());

        var vault = await OpenVaultAsync();

        Assert.Contains("apiKey", Assert.Single(vault.LoadWarnings), StringComparison.Ordinal);
        Assert.False(vault.TryGet(new SecretRef(SecretOwnerKind.Environment, env.Id, "apiKey"), out _));
        Assert.True(vault.TryGet(SecretRef.Globals("globalKey"), out var survivor));
        Assert.Equal(GlobalSecret, survivor);

        File.WriteAllText(_fx.VaultFile, "{ this is not json");
        var corrupt = ReadShared(_fx.VaultFile);

        var broken = await NewShellAsync();

        Assert.NotNull(broken.Vault.LastError);
        Assert.NotEqual(VaultState.Unlocked, broken.Vault.State);
        Assert.Equal(corrupt, ReadShared(_fx.VaultFile));
        Assert.Equal("", broken.Environments.Environments.Single().Variables.Rows.Single(r => r.Key == "apiKey").Value);
        Assert.NotNull(broken.Tabs.ActiveTab);
    }

    [Fact]
    public async Task DeletingDuplicatingAndMovingKeepTheVaultConsistent()
    {
        var inner = WriteCollection();
        _fx.WriteFolder("other", "Other");
        var shell = await NewShellAsync();
        await SetUpVaultAsync(shell);
        var env = await SeedAsync(shell, inner);

        shell.Environments.DuplicateCommand.Execute(env);
        var copy = shell.Environments.EditedEnvironment!;
        Assert.NotEqual(env.Id, copy.Id);
        Assert.Equal(ApiKey, copy.Variables.Rows.Single(r => r.Key == "apiKey").Value);
        await shell.Environments.SaveCommand.ExecuteAsync(copy);
        var vault = await OpenVaultAsync();
        Assert.True(vault.TryGet(new SecretRef(SecretOwnerKind.Environment, copy.Id, "apiKey"), out var copied));
        Assert.Equal(ApiKey, copied);

        var acme = shell.Collections.Roots.Single(r => r.Name == "Acme");
        var target = shell.Collections.GetMoveTargets(acme).Single(t => t.Path == "Other");
        await shell.Collections.MoveCommand.ExecuteAsync(new MoveRequest(acme, target));
        Assert.NotNull(shell.Collections.LastError);
        Assert.Contains(shell.Collections.Roots, r => r.Name == "Acme");
        Assert.Contains("secret", ReadText(_fx.PathOf("requests", "acme", "folder.yaml")), StringComparison.OrdinalIgnoreCase);

        await shell.Environments.DeleteConfirmedAsync(copy);
        await shell.Environments.DeleteConfirmedAsync(shell.Environments.Environments.Single(e => e.Id == env.Id));

        var owners = VaultEntries().Where(e => (string)e!["kind"]! == "Environment").ToList();
        Assert.Empty(owners);
        Assert.Equal(2, VaultEntries().Count);
    }
}
