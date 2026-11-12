using Yaapic.Core.Scripting;
using Yaapic.Core.Updates;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Updates;
using Yaapic.ViewModels.Vault;

namespace Yaapic.ViewModels.Tests;

/// <summary>The console, vault and engines every <see cref="Shell.ShellViewModel"/> needs, built from in-memory doubles.</summary>
internal sealed class ShellParts
{
    public ShellParts(ScriptEngineRegistry? engines = null)
    {
        Engines = engines ?? new ScriptEngineRegistry();
        Vault = new InMemorySecretVault();
        Updates = new UpdateViewModel(UpdateService, UpdateSettings, new AppInfo(AppVersion.Parse("1.0.0")), new UpdateScheduler(TimeProvider.System), Console);
        VaultViewModel = new VaultViewModel(Vault, new FakeKeyProtectorFactory(new FakeKeyProtector("passphrase", requiresPassphrase: true)), new NoReload());
    }

    public ScriptEngineRegistry Engines { get; }

    public InMemorySecretVault Vault { get; }

    public VaultViewModel VaultViewModel { get; }

    public ConsoleViewModel Console { get; } = new();

    public FakeUpdateService UpdateService { get; } = new() { IsSupported = false };

    public InMemoryUpdateSettingsStore UpdateSettings { get; } = new();

    public UpdateViewModel Updates { get; }

    private sealed class NoReload : IVaultReloader
    {
        public Task ReloadAsync() => Task.CompletedTask;
    }
}
