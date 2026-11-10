using Yaapic.Core.Secrets;
using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class SecretAwareStoreTests
{
    static readonly CancellationToken Ct = CancellationToken.None;

    static (SecretAwareEnvironmentStore Store, InMemoryEnvironmentStore Inner, InMemorySecretVault Vault) Env(VaultState state)
    {
        var inner = new InMemoryEnvironmentStore();
        var vault = new InMemorySecretVault();
        vault.Arrange(state);
        return (new(inner, vault), inner, vault);
    }

    static (SecretAwareGlobalsStore Store, InMemoryGlobalsStore Inner, InMemorySecretVault Vault) Globals(VaultState state)
    {
        var inner = new InMemoryGlobalsStore();
        var vault = new InMemorySecretVault();
        vault.Arrange(state);
        return (new(inner, vault), inner, vault);
    }

    static EnvironmentDefinition Environment(KeyValueEntry[] rows) => EnvironmentDefinition.Create("e") with { Variables = rows };

    static SecretRef Ref(EnvironmentDefinition e, string name) => new(SecretOwnerKind.Environment, e.Id, name);

    [Fact]
    public async Task Environment_save_unlocked_writes_vault_and_blanks_inner()
    {
        var (store, inner, vault) = Env(VaultState.Unlocked);
        var e = Environment([new("host", "h"), new("token", "s3cret", true, true)]);
        await store.SaveAsync(e, Ct);

        var stored = Assert.Single(await inner.ListAsync(Ct)).Variables;
        Assert.Equal(new KeyValueEntry("token", "", true, true), stored[1]);
        Assert.Equal("h", stored[0].Value);
        Assert.True(vault.TryGet(Ref(e, "token"), out var v));
        Assert.Equal("s3cret", v);
    }

    [Fact]
    public async Task Environment_load_fills_secret_values_and_leaves_others()
    {
        var (store, _, _) = Env(VaultState.Unlocked);
        var e = Environment([new("host", "h"), new("token", "s3cret", true, true)]);
        await store.SaveAsync(e, Ct);

        var loaded = Assert.Single(await store.ListAsync(Ct)).Variables;
        Assert.Equal(e.Variables, loaded);
    }

    [Fact]
    public async Task Environment_load_while_locked_gives_empty_secret_values()
    {
        var (store, _, vault) = Env(VaultState.Unlocked);
        await store.SaveAsync(Environment([new("host", "h"), new("token", "s3cret", true, true)]), Ct);
        vault.Lock();

        var loaded = Assert.Single(await store.ListAsync(Ct)).Variables;
        Assert.Equal("", loaded[1].Value);
        Assert.Equal("h", loaded[0].Value);
    }

    [Fact]
    public async Task Environment_save_locked_keeps_vault_untouched_and_blanks_secrets()
    {
        var (store, inner, vault) = Env(VaultState.Unlocked);
        var e = Environment([new("host", "h"), new("token", "s3cret", true, true)]);
        await store.SaveAsync(e, Ct);
        vault.Lock();
        vault.Calls.Clear();

        await store.SaveAsync(e with { Variables = [new("host", "h2"), new("token", "", true, true)] }, Ct);

        Assert.Empty(vault.Calls);
        Assert.Equal("h2", Assert.Single(await inner.ListAsync(Ct)).Variables[0].Value);
        vault.Arrange(VaultState.Unlocked);
        Assert.True(vault.TryGet(Ref(e, "token"), out var v));
        Assert.Equal("s3cret", v);
    }

    [Theory]
    [InlineData(VaultState.Locked)]
    [InlineData(VaultState.NotCreated)]
    public async Task Environment_save_refuses_new_secret_flag_when_not_unlocked(VaultState state)
    {
        var (store, inner, vault) = Env(state);
        var ex = await Assert.ThrowsAsync<StorageException>(() =>
            store.SaveAsync(Environment([new("token", "x", true, true)]), Ct));

        Assert.Equal("Unlock the vault to mark 'token' as secret", ex.Message);
        Assert.Empty(await inner.ListAsync(Ct));
        Assert.Empty(vault.Calls);
    }

    [Fact]
    public async Task Environment_removed_renamed_and_unflagged_rows_clean_the_vault()
    {
        var (store, inner, vault) = Env(VaultState.Unlocked);
        var e = Environment([new("a", "1", true, true), new("b", "2", true, true), new("c", "3", true, true)]);
        await store.SaveAsync(e, Ct);

        await store.SaveAsync(e with { Variables = [new("a2", "1", true, true), new("c", "3", true, false)] }, Ct);

        Assert.False(vault.TryGet(Ref(e, "a"), out _));
        Assert.False(vault.TryGet(Ref(e, "b"), out _));
        Assert.False(vault.TryGet(Ref(e, "c"), out _));
        Assert.True(vault.TryGet(Ref(e, "a2"), out _));
        var stored = Assert.Single(await inner.ListAsync(Ct)).Variables;
        Assert.Equal(new KeyValueEntry("c", "3", true, false), stored[1]);
    }

    [Fact]
    public async Task Environment_vault_failure_leaves_inner_untouched_and_throws_StorageException()
    {
        var (store, inner, vault) = Env(VaultState.Unlocked);
        var e = Environment([new("host", "h")]);
        await store.SaveAsync(e, Ct);
        var failingStore = new SecretAwareEnvironmentStore(inner, new ThrowingVault(vault));

        var ex = await Assert.ThrowsAsync<StorageException>(() =>
            failingStore.SaveAsync(e with { Variables = [new("host", "changed"), new("t", "v", true, true)] }, Ct));

        Assert.Equal("Cannot save secret 't': boom", ex.Message);
        Assert.Equal("h", Assert.Single(await inner.ListAsync(Ct)).Variables[0].Value);
    }

    [Fact]
    public async Task Environment_delete_removes_the_owner_secrets()
    {
        var (store, inner, vault) = Env(VaultState.Unlocked);
        var e = Environment([new("t", "v", true, true)]);
        await store.SaveAsync(e, Ct);

        await store.DeleteAsync(e.Id, Ct);

        Assert.Empty(await inner.ListAsync(Ct));
        Assert.False(vault.TryGet(Ref(e, "t"), out _));
    }

    [Fact]
    public async Task Duplicated_environment_keeps_secret_values_under_the_new_id()
    {
        var (store, _, vault) = Env(VaultState.Unlocked);
        var e = Environment([new("t", "v", true, true)]);
        await store.SaveAsync(e, Ct);
        var copy = Assert.Single(await store.ListAsync(Ct)) with { Id = Guid.NewGuid(), Name = "copy" };

        await store.SaveAsync(copy, Ct);

        Assert.True(vault.TryGet(Ref(copy, "t"), out var v));
        Assert.Equal("v", v);
        Assert.True(vault.TryGet(Ref(e, "t"), out _));
        Assert.All(await store.ListAsync(Ct), x => Assert.Equal("v", x.Variables[0].Value));
    }

    [Fact]
    public async Task Globals_save_and_load_round_trip_through_the_vault()
    {
        var (store, inner, vault) = Globals(VaultState.Unlocked);
        await store.SaveAsync([new("a", "1"), new("k", "secret", true, true)], Ct);

        Assert.Equal(new KeyValueEntry("k", "", true, true), (await inner.LoadAsync(Ct))[1]);
        Assert.True(vault.TryGet(SecretRef.Globals("k"), out var v));
        Assert.Equal("secret", v);
        var loaded = await store.LoadAsync(Ct);
        Assert.Equal("secret", loaded[1].Value);
        Assert.Equal("1", loaded[0].Value);
    }

    [Fact]
    public async Task Globals_load_while_locked_gives_empty_values_and_save_keeps_the_vault_untouched()
    {
        var (store, inner, vault) = Globals(VaultState.Unlocked);
        await store.SaveAsync([new("k", "secret", true, true)], Ct);
        vault.Lock();
        vault.Calls.Clear();

        Assert.Equal("", (await store.LoadAsync(Ct))[0].Value);
        await store.SaveAsync([new("k", "", true, true), new("n", "1")], Ct);

        Assert.Empty(vault.Calls);
        Assert.Equal(2, (await inner.LoadAsync(Ct)).Count);
    }

    [Fact]
    public async Task Globals_locked_save_refuses_new_secret_flag()
    {
        var (store, inner, _) = Globals(VaultState.Locked);
        var ex = await Assert.ThrowsAsync<StorageException>(() => store.SaveAsync([new("k", "v", true, true)], Ct));
        Assert.Equal("Unlock the vault to mark 'k' as secret", ex.Message);
        Assert.Empty(await inner.LoadAsync(Ct));
    }

    [Fact]
    public async Task Globals_removed_and_unflagged_rows_clean_the_vault()
    {
        var (store, _, vault) = Globals(VaultState.Unlocked);
        await store.SaveAsync([new("a", "1", true, true), new("b", "2", true, true)], Ct);
        await store.SaveAsync([new("b", "2", true, false)], Ct);

        Assert.False(vault.TryGet(SecretRef.Globals("a"), out _));
        Assert.False(vault.TryGet(SecretRef.Globals("b"), out _));
    }

    [Fact]
    public async Task Globals_vault_failure_leaves_inner_untouched()
    {
        var (_, inner, vault) = Globals(VaultState.Unlocked);
        await inner.SaveAsync([new("x", "1")], Ct);
        var store = new SecretAwareGlobalsStore(inner, new ThrowingVault(vault));

        await Assert.ThrowsAsync<StorageException>(() => store.SaveAsync([new("k", "v", true, true)], Ct));

        Assert.Equal("x", Assert.Single(await inner.LoadAsync(Ct)).Key);
    }

    sealed class ThrowingVault(InMemorySecretVault real) : ISecretVault
    {
        public VaultState State => real.State;
        public string? ProtectorId => real.ProtectorId;
        public event EventHandler? StateChanged { add { } remove { } }
        public Task InitializeAsync(CancellationToken ct) => real.InitializeAsync(ct);
        public Task CreateAsync(string protectorId, string? passphrase, CancellationToken ct) => real.CreateAsync(protectorId, passphrase, ct);
        public Task<bool> UnlockAsync(string passphrase, CancellationToken ct) => real.UnlockAsync(passphrase, ct);
        public void Lock() => real.Lock();
        public bool TryGet(SecretRef secret, out string value) => real.TryGet(secret, out value);
        public Task SetAsync(SecretRef secret, string value, CancellationToken ct) => throw new VaultException("boom");
        public Task RemoveAsync(SecretRef secret, CancellationToken ct) => real.RemoveAsync(secret, ct);
        public Task RemoveOwnerAsync(SecretOwnerKind kind, Guid ownerId, CancellationToken ct) => real.RemoveOwnerAsync(kind, ownerId, ct);
        public IReadOnlyList<string> GetSecretValues() => real.GetSecretValues();
    }
}
