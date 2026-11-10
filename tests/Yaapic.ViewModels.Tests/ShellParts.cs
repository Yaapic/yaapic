using Yaapic.Core.Scripting;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Vault;

namespace Yaapic.ViewModels.Tests;

/// <summary>The console, vault and engines every <see cref="Shell.ShellViewModel"/> needs, built from in-memory doubles.</summary>
internal sealed class ShellParts
{
    public ShellParts(ScriptEngineRegistry? engines = null)
    {
        Engines = engines ?? new ScriptEngineRegistry();
        Vault = new InMemorySecretVault();
        VaultViewModel = new VaultViewModel(Vault, new FakeKeyProtectorFactory(new FakeKeyProtector("passphrase", requiresPassphrase: true)), new NoReload());
    }

    public ScriptEngineRegistry Engines { get; }

    public InMemorySecretVault Vault { get; }

    public VaultViewModel VaultViewModel { get; }

    public ConsoleViewModel Console { get; } = new();

    private sealed class NoReload : IVaultReloader
    {
        public Task ReloadAsync() => Task.CompletedTask;
    }
}
