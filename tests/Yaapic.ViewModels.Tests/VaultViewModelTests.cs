using Yaapic.Core.Secrets;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Vault;

namespace Yaapic.ViewModels.Tests;

public sealed class VaultViewModelTests
{
    private readonly InMemorySecretVault _vault = new();
    private readonly RecordingReloader _reloader = new();

    [Theory]
    [InlineData(VaultState.NotCreated, "No vault")]
    [InlineData(VaultState.Locked, "Vault locked")]
    [InlineData(VaultState.Unlocked, "Vault unlocked")]
    public void StatusTextFollowsTheState(VaultState state, string text)
    {
        _vault.Arrange(state);
        var vm = Create();

        Assert.Equal(text, vm.StatusText);
        Assert.Equal(state == VaultState.NotCreated, vm.CanSetUp);
        Assert.Equal(state == VaultState.Locked, vm.NeedsPassphrase);
        Assert.Equal(state == VaultState.Unlocked, vm.IsUnlocked);
    }

    [Theory]
    [InlineData("dpapi", "Windows account (DPAPI)")]
    [InlineData("keychain", "macOS Keychain")]
    [InlineData("secret-service", "Secret Service")]
    [InlineData("passphrase", "Passphrase")]
    public void ProtectorLabelNamesTheVaultProtector(string id, string label)
    {
        _vault.Arrange(VaultState.Locked, id);

        Assert.Equal(label, Create().ProtectorLabel);
    }

    [Fact]
    public void ProtectorLabelIsNullWithoutVault() => Assert.Null(Create().ProtectorLabel);

    [Fact]
    public void ProtectorsListThePlatformOneFirstAndPassphraseLast()
    {
        var vm = Create(new FakeKeyProtector("passphrase", true), new FakeKeyProtector("dpapi"));

        Assert.Equal(["dpapi", "passphrase"], vm.Protectors.Select(p => p.Id));
        Assert.Equal("dpapi", vm.SelectedProtector!.Id);
        Assert.True(vm.Protectors[1].RequiresPassphrase);
    }

    [Theory]
    [InlineData("passphrase", "short", "short", false)]
    [InlineData("passphrase", "long-enough", "different", false)]
    [InlineData("passphrase", "long-enough", "long-enough", true)]
    [InlineData("dpapi", "", "", true)]
    public void SetupValidation(string protector, string passphrase, string confirm, bool expected)
    {
        var vm = Create();
        vm.SelectedProtector = vm.Protectors.Single(p => p.Id == protector);
        vm.Passphrase = passphrase;
        vm.ConfirmPassphrase = confirm;

        Assert.Equal(expected, vm.CanSubmitSetup);
        Assert.Equal(expected, vm.SetupCommand.CanExecute(null));
    }

    [Fact]
    public async Task SetupWithOsProtectorCreatesTheVaultAndReloadsOnce()
    {
        var vm = Create();

        await vm.SetupCommand.ExecuteAsync(null);

        Assert.Contains("CreateAsync:dpapi", _vault.Calls);
        Assert.Equal(VaultState.Unlocked, vm.State);
        Assert.Equal("Vault unlocked", vm.StatusText);
        Assert.Equal(1, _reloader.Count);
    }

    [Fact]
    public async Task SetupWithPassphraseClearsTheFields()
    {
        var vm = Create();
        vm.SelectedProtector = vm.Protectors[1];
        vm.Passphrase = vm.ConfirmPassphrase = "long-enough";

        await vm.SetupCommand.ExecuteAsync(null);

        Assert.Equal("", vm.Passphrase);
        Assert.Equal("", vm.ConfirmPassphrase);
        Assert.Equal(1, _reloader.Count);
    }

    [Fact]
    public async Task SetupFailureShowsTheErrorWithoutReload()
    {
        _vault.Arrange(VaultState.Unlocked);
        var vm = Create();

        await vm.SetupCommand.ExecuteAsync(null);

        Assert.Equal("The vault already exists", vm.LastError);
        Assert.Equal(0, _reloader.Count);
    }

    [Fact]
    public async Task UnlockWithWrongPassphraseReportsItAndClearsTheField()
    {
        _vault.Arrange(VaultState.Locked, passphrase: "right-pass");
        var vm = Create();
        vm.UnlockPassphrase = "wrong";

        await vm.UnlockCommand.ExecuteAsync(null);

        Assert.Equal("Wrong passphrase", vm.LastError);
        Assert.Equal("", vm.UnlockPassphrase);
        Assert.Equal(VaultState.Locked, vm.State);
        Assert.Equal(0, _reloader.Count);
    }

    [Fact]
    public async Task UnlockSucceedsReloadsOnceAndClearsTheField()
    {
        _vault.Arrange(VaultState.Locked, passphrase: "right-pass");
        var vm = Create();
        vm.UnlockPassphrase = "right-pass";

        await vm.UnlockCommand.ExecuteAsync(null);

        Assert.Null(vm.LastError);
        Assert.True(vm.IsUnlocked);
        Assert.Equal("", vm.UnlockPassphrase);
        Assert.Equal(1, _reloader.Count);
    }

    [Fact]
    public async Task LockReloadsOnceAndClearsThePassphrase()
    {
        _vault.Arrange(VaultState.Unlocked);
        var vm = Create();
        vm.UnlockPassphrase = "left-over";

        await vm.LockCommand.ExecuteAsync(null);

        Assert.Equal(VaultState.Locked, vm.State);
        Assert.Equal("", vm.UnlockPassphrase);
        Assert.Equal(1, _reloader.Count);
    }

    [Fact]
    public async Task LockWhenAlreadyLockedDoesNotReload()
    {
        _vault.Arrange(VaultState.Locked);
        var vm = Create();

        await vm.LockCommand.ExecuteAsync(null);

        Assert.Equal(0, _reloader.Count);
    }

    [Fact]
    public async Task InitializeReloadsOnce()
    {
        _vault.Arrange(VaultState.Locked);
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Contains("InitializeAsync", _vault.Calls);
        Assert.Equal(1, _reloader.Count);
    }

    [Fact]
    public async Task IsBusyWhileTheVaultIsSlow()
    {
        var gate = new TaskCompletionSource();
        var slow = new SlowVault(gate.Task);
        var vm = new VaultViewModel(slow, new FakeKeyProtectorFactory(new FakeKeyProtector("dpapi")), _reloader);

        var run = vm.SetupCommand.ExecuteAsync(null);

        Assert.True(vm.IsBusy);
        Assert.False(vm.CanSubmitSetup);
        gate.SetResult();
        await run;
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task RequestCommandsRaiseTheEventOfTheState()
    {
        var vm = Create();
        var raised = new List<string>();
        vm.SetupRequested += (_, _) => raised.Add("setup");
        vm.UnlockRequested += (_, _) => raised.Add("unlock");

        vm.RequestSetupCommand.Execute(null);
        Assert.False(vm.RequestUnlockCommand.CanExecute(null));

        _vault.Arrange(VaultState.Locked);
        await vm.InitializeAsync();
        vm.RequestUnlockCommand.Execute(null);
        Assert.False(vm.RequestSetupCommand.CanExecute(null));

        Assert.Equal(["setup", "unlock"], raised);
    }

    private VaultViewModel Create(params FakeKeyProtector[] protectors) =>
        new(_vault, new FakeKeyProtectorFactory(protectors.Length > 0 ? protectors : [new FakeKeyProtector("dpapi"), new FakeKeyProtector("passphrase", true)]), _reloader);

    private sealed class RecordingReloader : IVaultReloader
    {
        public int Count { get; private set; }

        public Task ReloadAsync()
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class SlowVault(Task gate) : ISecretVault
    {
        private readonly InMemorySecretVault _inner = new();

        public VaultState State => _inner.State;

        public string? ProtectorId => _inner.ProtectorId;

        public event EventHandler? StateChanged
        {
            add => _inner.StateChanged += value;
            remove => _inner.StateChanged -= value;
        }

        public Task InitializeAsync(CancellationToken ct) => _inner.InitializeAsync(ct);

        public async Task CreateAsync(string protectorId, string? passphrase, CancellationToken ct)
        {
            await gate;
            await _inner.CreateAsync(protectorId, passphrase, ct);
        }

        public Task<bool> UnlockAsync(string passphrase, CancellationToken ct) => _inner.UnlockAsync(passphrase, ct);

        public void Lock() => _inner.Lock();

        public bool TryGet(SecretRef secret, out string value) => _inner.TryGet(secret, out value);

        public Task SetAsync(SecretRef secret, string value, CancellationToken ct) => _inner.SetAsync(secret, value, ct);

        public Task RemoveAsync(SecretRef secret, CancellationToken ct) => _inner.RemoveAsync(secret, ct);

        public Task RemoveOwnerAsync(SecretOwnerKind kind, Guid ownerId, CancellationToken ct) => _inner.RemoveOwnerAsync(kind, ownerId, ct);

        public IReadOnlyList<string> GetSecretValues() => _inner.GetSecretValues();
    }
}
