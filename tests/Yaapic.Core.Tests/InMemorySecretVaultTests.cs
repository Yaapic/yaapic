using Yaapic.Core.Secrets;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class InMemorySecretVaultTests
{
    static readonly CancellationToken Ct = CancellationToken.None;
    static readonly Guid Env = Guid.NewGuid();

    static async Task<InMemorySecretVault> Unlocked()
    {
        var vault = new InMemorySecretVault();
        await vault.CreateAsync("passphrase", "pw", Ct);
        return vault;
    }

    [Fact]
    public async Task Transitions_follow_create_lock_unlock_and_raise_once_each()
    {
        var vault = new InMemorySecretVault();
        var raised = 0;
        vault.StateChanged += (_, _) => raised++;
        Assert.Equal(VaultState.NotCreated, vault.State);
        Assert.Null(vault.ProtectorId);

        await vault.CreateAsync("passphrase", "pw", Ct);
        Assert.Equal((VaultState.Unlocked, 1), (vault.State, raised));
        Assert.Equal("passphrase", vault.ProtectorId);

        vault.Lock();
        vault.Lock();
        Assert.Equal((VaultState.Locked, 2), (vault.State, raised));

        Assert.True(await vault.UnlockAsync("pw", Ct));
        Assert.Equal((VaultState.Unlocked, 3), (vault.State, raised));
    }

    [Fact]
    public async Task Wrong_passphrase_returns_false_and_stays_locked()
    {
        var vault = await Unlocked();
        vault.Lock();
        var raised = 0;
        vault.StateChanged += (_, _) => raised++;

        Assert.False(await vault.UnlockAsync("nope", Ct));
        Assert.Equal((VaultState.Locked, 0), (vault.State, raised));
    }

    [Fact]
    public async Task Set_get_remove_round_trip()
    {
        var vault = await Unlocked();
        var key = new SecretRef(SecretOwnerKind.Environment, Env, "token");

        await vault.SetAsync(key, "abc123", Ct);
        Assert.True(vault.TryGet(key, out var value));
        Assert.Equal("abc123", value);

        await vault.RemoveAsync(key, Ct);
        Assert.False(vault.TryGet(key, out _));
    }

    [Fact]
    public async Task Remove_owner_only_removes_that_owner()
    {
        var vault = await Unlocked();
        var mine = new SecretRef(SecretOwnerKind.Environment, Env, "a");
        var other = new SecretRef(SecretOwnerKind.Environment, Guid.NewGuid(), "a");
        var global = SecretRef.Globals("a");
        foreach (var s in new[] { mine, other, global }) await vault.SetAsync(s, "value", Ct);

        await vault.RemoveOwnerAsync(SecretOwnerKind.Environment, Env, Ct);

        Assert.False(vault.TryGet(mine, out _));
        Assert.True(vault.TryGet(other, out _));
        Assert.True(vault.TryGet(global, out _));
    }

    [Fact]
    public async Task Locked_writes_throw_and_reads_miss()
    {
        var vault = await Unlocked();
        var key = SecretRef.Globals("g");
        await vault.SetAsync(key, "value", Ct);
        vault.Lock();

        var ex = await Assert.ThrowsAsync<VaultException>(() => vault.SetAsync(key, "x", Ct));
        Assert.Equal("The vault is locked", ex.Message);
        await Assert.ThrowsAsync<VaultException>(() => vault.RemoveAsync(key, Ct));
        await Assert.ThrowsAsync<VaultException>(() => vault.RemoveOwnerAsync(SecretOwnerKind.Globals, Guid.Empty, Ct));
        Assert.False(vault.TryGet(key, out _));
        Assert.Empty(vault.GetSecretValues());
    }

    [Fact]
    public async Task Secret_values_are_longest_first_with_a_length_four_floor()
    {
        var vault = await Unlocked();
        vault.Seed(SecretRef.Globals("a"), "abc");
        vault.Seed(SecretRef.Globals("b"), "abcd");
        vault.Seed(SecretRef.Globals("c"), "abcdefgh");
        vault.Seed(SecretRef.Globals("d"), "wxyz");

        Assert.Equal(["abcdefgh", "abcd", "wxyz"], vault.GetSecretValues());
    }

    [Fact]
    public void Globals_ref_has_an_empty_owner()
    {
        var key = SecretRef.Globals("x");
        Assert.Equal((SecretOwnerKind.Globals, Guid.Empty, "x"), (key.Kind, key.OwnerId, key.Name));
    }
}
