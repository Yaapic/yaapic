using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Secrets;
using Yaapic.Core.Storage;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class SecretRedactionTests
{
    static readonly CancellationToken Ct = CancellationToken.None;

    static (SecretAwareCollectionStore Store, InMemoryCollectionStore Inner, InMemorySecretVault Vault) Build(VaultState state = VaultState.Unlocked)
    {
        var inner = new InMemoryCollectionStore(new InMemoryRequestStore());
        var vault = new InMemorySecretVault();
        vault.Arrange(state);
        return (new(inner, vault), inner, vault);
    }

    static FolderDefinition Folder(params KeyValueEntry[] rows) => FolderDefinition.Create("f") with { Variables = rows };

    static SecretRef Ref(FolderDefinition f, string name) => new(SecretOwnerKind.Collection, f.Id, name);

    static async Task<IReadOnlyList<KeyValueEntry>> Stored(ICollectionStore store, Guid id) =>
        CollectionNode.Flatten(await store.LoadTreeAsync(Ct)).First(n => n.Id == id).Folder!.Variables;

    [Fact]
    public async Task Top_level_save_writes_vault_blanks_inner_and_load_fills()
    {
        var (store, inner, vault) = Build();
        var f = Folder(new KeyValueEntry("host", "h"), new("token", "s3cret", true, true));
        await store.SaveFolderAsync(f, null, Ct);

        Assert.Equal(new KeyValueEntry("token", "", true, true), (await Stored(inner, f.Id))[1]);
        Assert.True(vault.TryGet(Ref(f, "token"), out var v));
        Assert.Equal("s3cret", v);
        Assert.Equal(f.Variables, await Stored(store, f.Id));
    }

    [Fact]
    public async Task Load_while_locked_gives_empty_secret_values()
    {
        var (store, _, vault) = Build();
        var f = Folder(new KeyValueEntry("token", "s3cret", true, true));
        await store.SaveFolderAsync(f, null, Ct);
        vault.Lock();

        Assert.Equal("", (await Stored(store, f.Id))[0].Value);
    }

    [Fact]
    public async Task Save_refuses_new_secret_flag_when_not_unlocked()
    {
        var (store, inner, _) = Build(VaultState.Locked);
        var ex = await Assert.ThrowsAsync<StorageException>(() =>
            store.SaveFolderAsync(Folder(new KeyValueEntry("token", "x", true, true)), null, Ct));

        Assert.Equal("Unlock the vault to mark 'token' as secret", ex.Message);
        Assert.Empty(await inner.LoadTreeAsync(Ct));
    }

    [Fact]
    public async Task Removed_rows_clean_the_vault()
    {
        var (store, _, vault) = Build();
        var f = Folder(new KeyValueEntry("a", "1111", true, true));
        await store.SaveFolderAsync(f, null, Ct);
        await store.SaveFolderAsync(f with { Variables = [] }, null, Ct);

        Assert.False(vault.TryGet(Ref(f, "a"), out _));
    }

    [Fact]
    public async Task Nested_folder_passes_through_unchanged()
    {
        var (store, inner, vault) = Build();
        var parent = Folder();
        await store.SaveFolderAsync(parent, null, Ct);
        var child = Folder(new KeyValueEntry("k", "plain", true, true));
        await store.SaveFolderAsync(child, parent.Id, Ct);

        Assert.Equal(child.Variables, await Stored(inner, child.Id));
        Assert.DoesNotContain(vault.Calls, c => c.StartsWith("SetAsync", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Delete_top_level_removes_owner_secrets_but_nested_touches_nothing()
    {
        var (store, _, vault) = Build();
        var top = Folder(new KeyValueEntry("t", "1111", true, true));
        var child = Folder();
        await store.SaveFolderAsync(top, null, Ct);
        await store.SaveFolderAsync(child, top.Id, Ct);
        vault.Calls.Clear();

        await store.DeleteFolderAsync(child.Id, Ct);
        Assert.Empty(vault.Calls);

        await store.DeleteFolderAsync(top.Id, Ct);
        Assert.False(vault.TryGet(Ref(top, "t"), out _));
    }

    [Fact]
    public async Task Move_of_a_folder_with_secrets_across_the_top_level_is_refused()
    {
        var (store, _, _) = Build();
        var secret = Folder(new KeyValueEntry("t", "1111", true, true));
        var other = Folder();
        await store.SaveFolderAsync(secret, null, Ct);
        await store.SaveFolderAsync(other, null, Ct);

        var ex = await Assert.ThrowsAsync<StorageException>(() => store.MoveAsync(secret.Id, other.Id, Ct));
        Assert.Equal("Cannot move a collection that has secret variables", ex.Message);
    }

    [Fact]
    public async Task Move_without_secrets_passes_through()
    {
        var (store, _, _) = Build();
        var a = Folder(new KeyValueEntry("t", "1"));
        var b = Folder();
        await store.SaveFolderAsync(a, null, Ct);
        await store.SaveFolderAsync(b, null, Ct);

        await store.MoveAsync(a.Id, b.Id, Ct);

        Assert.Equal(a.Id, Assert.Single(Assert.Single(await store.LoadTreeAsync(Ct)).Children).Id);
    }

    static SecretRedactor Redactor(params string[] secrets)
    {
        var vault = new InMemorySecretVault();
        vault.Arrange(VaultState.Unlocked);
        for (var i = 0; i < secrets.Length; i++)
        {
            vault.Seed(new(SecretOwnerKind.Globals, Guid.Empty, $"k{i}"), secrets[i]);
        }

        return new(vault);
    }

    [Theory]
    [InlineData("nothing here", "nothing here")]
    [InlineData("token=abcdef!", "token=••••!")]
    [InlineData("abcdef and abcdef", "•••• and ••••")]
    [InlineData("ABCDEF", "ABCDEF")]
    [InlineData("", "")]
    public void Redact_replaces_exact_occurrences(string text, string expected) =>
        Assert.Equal(expected, Redactor("abcdef").Redact(text));

    [Fact]
    public void Redact_handles_several_overlapping_and_short_secrets()
    {
        var redactor = Redactor("pass", "pass-word-long", "xyz", "other1");

        Assert.Equal("•••• / •••• / xyz / ••••", redactor.Redact("pass / pass-word-long / xyz / other1"));
    }

    [Fact]
    public void Redact_is_a_no_op_with_a_locked_vault_and_keeps_null()
    {
        var vault = new InMemorySecretVault();
        vault.Arrange(VaultState.Unlocked);
        vault.Seed(new(SecretOwnerKind.Globals, Guid.Empty, "k"), "abcdef");
        vault.Arrange(VaultState.Locked);
        var redactor = new SecretRedactor(vault);

        Assert.Equal("abcdef", redactor.Redact("abcdef"));
        Assert.Null(redactor.Redact(null!));
    }

    [Fact]
    public void ScriptRedaction_covers_console_tests_and_errors()
    {
        var r = Redactor("abcdef");
        var run = new ScriptRunResult(
            ScriptPhase.PostResponse, "o abcdef", ScriptLanguage.JavaScript, ScriptRunStatus.Failed,
            new ScriptError("e abcdef", 1, 2, "at abcdef"),
            [new(DateTimeOffset.UnixEpoch, ConsoleLevel.Log, ConsoleSource.Script, "log abcdef")],
            [new("t abcdef", false, "m abcdef", TimeSpan.Zero, "o")],
            TimeSpan.Zero);

        var redacted = ScriptRedaction.Redact(r, run);

        Assert.Equal("e ••••", redacted.Error!.Message);
        Assert.Equal("at ••••", redacted.Error.Stack);
        Assert.Equal("log ••••", Assert.Single(redacted.Console).Message);
        var test = Assert.Single(redacted.Tests);
        Assert.Equal(("t ••••", "m ••••"), (test.Name, test.Message));
    }

    [Fact]
    public async Task Sender_redacts_console_tests_and_network_log_but_the_transport_gets_the_real_value()
    {
        const string Secret = "s3cretValue";
        var vault = new InMemorySecretVault();
        vault.Arrange(VaultState.Unlocked);
        vault.Seed(new(SecretOwnerKind.Globals, Guid.Empty, "k"), Secret);
        var engine = new FakeScriptEngine(i => new ScriptRunResult(
            i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Succeeded, null,
            [new(DateTimeOffset.UnixEpoch, ConsoleLevel.Log, ConsoleSource.Script, $"log {Secret}")],
            [new($"test {Secret}", false, $"failed {Secret}", TimeSpan.Zero, i.Origin)],
            TimeSpan.Zero));
        ScriptEngineRegistry registry = new();
        registry.Add(engine);
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, $"down {Secret}")));
        var sender = new ScriptedRequestSender(new RequestSender(transport), registry, TimeProvider.System, new SecretRedactor(vault));
        var chain = new ScriptChain([new("req", ScriptSet.Empty with { PreRequest = new(ScriptLanguage.JavaScript, "x();") })]);
        var request = HttpRequestDefinition.Create("r") with { Url = "http://a/{{k}}" };
        var scopes = new VariableScopes(null, [], [new("k", Secret)]);

        var report = await sender.SendAsync(new(request, scopes, chain, false, false), Ct);

        Assert.Contains(Secret, Assert.Single(transport.Requests).Url, StringComparison.Ordinal);
        Assert.Contains("down ••••", Assert.Single(report.NetworkLog, l => l.Source == ConsoleSource.Network).Message, StringComparison.Ordinal);
        Assert.Equal("log ••••", Assert.Single(report.Console).Message);
        Assert.Equal("failed ••••", Assert.Single(report.Tests).Message);
    }

    [Fact]
    public async Task Sender_redacts_the_aborting_pre_request_run()
    {
        const string Secret = "s3cretValue";
        var vault = new InMemorySecretVault();
        vault.Arrange(VaultState.Unlocked);
        vault.Seed(new(SecretOwnerKind.Globals, Guid.Empty, "k"), Secret);
        var engine = new FakeScriptEngine(i => new ScriptRunResult(
            i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Failed, new ScriptError($"threw {Secret}"),
            [new(DateTimeOffset.UnixEpoch, ConsoleLevel.Log, ConsoleSource.Script, $"log {Secret}")],
            [new($"t {Secret}", false, $"m {Secret}", TimeSpan.Zero, "o")], TimeSpan.Zero));
        ScriptEngineRegistry registry = new();
        registry.Add(engine);
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "none")));
        var sender = new ScriptedRequestSender(new RequestSender(transport), registry, TimeProvider.System, new SecretRedactor(vault));
        var chain = new ScriptChain([new("req", ScriptSet.Empty with { PreRequest = new(ScriptLanguage.JavaScript, "x();") })]);

        var report = await sender.SendAsync(new(HttpRequestDefinition.Create("r"), VariableScopes.None, chain, false, false), Ct);

        Assert.Empty(transport.Requests);
        Assert.Equal("threw ••••", report.AbortedBy!.Error!.Message);
        Assert.Equal("log ••••", Assert.Single(report.Console).Message);
        var test = Assert.Single(report.Tests);
        Assert.Equal(("t ••••", "m ••••"), (test.Name, test.Message));
    }
}
