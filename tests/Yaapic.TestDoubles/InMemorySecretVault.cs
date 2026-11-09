using Yaapic.Core.Secrets;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="ISecretVault"/> kept in memory, with a configurable passphrase and a call log.</summary>
public sealed class InMemorySecretVault : ISecretVault
{
    private readonly Dictionary<SecretRef, string> _secrets = [];
    private string? _passphrase;

    /// <summary>Gets the calls made, as <c>Method</c> or <c>Method:argument</c> entries, in order.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc/>
    public VaultState State { get; private set; }

    /// <inheritdoc/>
    public string? ProtectorId { get; private set; }

    /// <inheritdoc/>
    public event EventHandler? StateChanged;

    /// <summary>Puts the vault in a state without raising <see cref="StateChanged"/>, with the passphrase to accept.</summary>
    /// <param name="state">The state.</param>
    /// <param name="protectorId">The protector identifier.</param>
    /// <param name="passphrase">The passphrase <see cref="UnlockAsync"/> accepts.</param>
    public void Arrange(VaultState state, string protectorId = "passphrase", string? passphrase = null)
    {
        State = state;
        ProtectorId = state == VaultState.NotCreated ? null : protectorId;
        _passphrase = passphrase;
    }

    /// <summary>Stores a secret directly, whatever the state, without recording a call.</summary>
    /// <param name="secret">The secret reference.</param>
    /// <param name="value">The plain value.</param>
    public void Seed(SecretRef secret, string value) => _secrets[secret] = value;

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken ct)
    {
        Calls.Add("InitializeAsync");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CreateAsync(string protectorId, string? passphrase, CancellationToken ct)
    {
        Calls.Add($"CreateAsync:{protectorId}");
        if (State != VaultState.NotCreated)
        {
            throw new VaultException("The vault already exists");
        }

        ProtectorId = protectorId;
        _passphrase = passphrase;
        ChangeState(VaultState.Unlocked);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> UnlockAsync(string passphrase, CancellationToken ct)
    {
        Calls.Add("UnlockAsync");
        if (State == VaultState.NotCreated)
        {
            throw new VaultException("The vault does not exist");
        }

        if (passphrase != _passphrase)
        {
            return Task.FromResult(false);
        }

        if (State == VaultState.Locked)
        {
            ChangeState(VaultState.Unlocked);
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public void Lock()
    {
        Calls.Add("Lock");
        if (State == VaultState.Unlocked)
        {
            ChangeState(VaultState.Locked);
        }
    }

    /// <inheritdoc/>
    public bool TryGet(SecretRef secret, out string value)
    {
        value = "";
        return State == VaultState.Unlocked && _secrets.TryGetValue(secret, out value!);
    }

    /// <inheritdoc/>
    public Task SetAsync(SecretRef secret, string value, CancellationToken ct)
    {
        Calls.Add($"SetAsync:{secret.Name}");
        RequireUnlocked();
        _secrets[secret] = value;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveAsync(SecretRef secret, CancellationToken ct)
    {
        Calls.Add($"RemoveAsync:{secret.Name}");
        RequireUnlocked();
        _secrets.Remove(secret);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RemoveOwnerAsync(SecretOwnerKind kind, Guid ownerId, CancellationToken ct)
    {
        Calls.Add($"RemoveOwnerAsync:{kind}:{ownerId}");
        RequireUnlocked();
        foreach (var key in _secrets.Keys.Where(k => k.Kind == kind && k.OwnerId == ownerId).ToList())
        {
            _secrets.Remove(key);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetSecretValues() => State != VaultState.Unlocked
        ? []
        : [.. _secrets.Values.Where(v => v.Length >= 4).Distinct().OrderByDescending(v => v.Length).ThenBy(v => v, StringComparer.Ordinal)];

    private void RequireUnlocked()
    {
        if (State != VaultState.Unlocked)
        {
            throw new VaultException("The vault is locked");
        }
    }

    private void ChangeState(VaultState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
