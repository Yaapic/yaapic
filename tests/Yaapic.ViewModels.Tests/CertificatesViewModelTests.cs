using Yaapic.Core.Secrets;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Settings;

namespace Yaapic.ViewModels.Tests;

public sealed class CertificatesViewModelTests
{
    private readonly InMemoryCertificateStore _store = new();
    private readonly InMemorySecretVault _vault = new();
    private readonly FakeDialogService _dialogs = new();

    public CertificatesViewModelTests() => _vault.Arrange(VaultState.Unlocked);

    private CertificatesViewModel Create() => new(_store, _vault, _dialogs);

    private static ClientCertificate Entry(string name, string host = "api.example.com", bool hasPassphrase = false) =>
        new(Guid.NewGuid(), name, host, ClientCertificateFormat.Pfx, "c.pfx", "", hasPassphrase);

    private static void Fill(CertificateItemViewModel item, string name = "Mine")
    {
        item.Name = name;
        item.HostPattern = "api.example.com";
        item.CertificatePath = "c.pfx";
    }

    private async Task<CertificatesViewModel> LoadedAsync(params ClientCertificate[] entries)
    {
        await _store.SaveAsync(entries, CancellationToken.None);
        var vm = Create();
        await vm.LoadAsync();
        return vm;
    }

    [Fact]
    public async Task AddEditSaveAndReload()
    {
        var vm = Create();
        await vm.LoadAsync();

        vm.AddCommand.Execute(null);
        var item = vm.Selected!;
        Assert.Equal("New certificate", item.Name);
        Assert.Equal(ClientCertificateFormat.Pfx, item.Format);
        Fill(item);
        await vm.SaveCommand.ExecuteAsync(null);

        var reloaded = Create();
        await reloaded.LoadAsync();
        var saved = Assert.Single(reloaded.Items);
        Assert.Equal((item.Id, "Mine", "api.example.com", "c.pfx"), (saved.Id, saved.Name, saved.HostPattern, saved.CertificatePath));
    }

    [Fact]
    public async Task InvalidEntriesBlockTheSaveAndListIssues()
    {
        var vm = Create();
        await vm.LoadAsync();
        vm.AddCommand.Execute(null);
        vm.Selected!.Name = "";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(3, vm.Selected.Issues.Count);
        Assert.Empty(await _store.LoadAsync(CancellationToken.None));
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task PassphraseGoesToTheVaultAndIsClearedAfterSave()
    {
        var vm = Create();
        await vm.LoadAsync();
        vm.AddCommand.Execute(null);
        var item = vm.Selected!;
        Fill(item);
        item.Passphrase = "s3cret";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(_vault.TryGet(SecretRef.Certificate(item.Id), out var stored));
        Assert.Equal("s3cret", stored);
        Assert.Equal("", item.Passphrase);
        Assert.True(item.HasPassphrase);
        Assert.True((await _store.LoadAsync(CancellationToken.None))[0].HasPassphrase);
    }

    [Theory]
    [InlineData(VaultState.Locked)]
    [InlineData(VaultState.NotCreated)]
    public async Task LockedVaultWithATypedPassphraseBlocksTheSave(VaultState state)
    {
        _vault.Arrange(state);
        var vm = Create();
        await vm.LoadAsync();
        vm.AddCommand.Execute(null);
        Fill(vm.Selected!);
        vm.Selected!.Passphrase = "s3cret";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Unlock the vault to store or remove a certificate passphrase.", vm.ErrorMessage);
        Assert.Empty(await _store.LoadAsync(CancellationToken.None));
        Assert.DoesNotContain(_vault.Calls, c => c.StartsWith("SetAsync", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LockedVaultDoesNotBlockSavingWithoutPassphrase()
    {
        _vault.Arrange(VaultState.Locked);
        var vm = Create();
        await vm.LoadAsync();
        vm.AddCommand.Execute(null);
        Fill(vm.Selected!);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.Single(await _store.LoadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task RemovePassphraseFlagRemovesTheSecret()
    {
        var entry = Entry("A", hasPassphrase: true);
        _vault.Seed(SecretRef.Certificate(entry.Id), "old");
        var vm = await LoadedAsync(entry);
        vm.Selected!.RemovePassphrase = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains($"RemoveAsync:passphrase", _vault.Calls);
        Assert.False(vm.Selected.HasPassphrase);
        Assert.False(vm.Selected.RemovePassphrase);
        Assert.False((await _store.LoadAsync(CancellationToken.None))[0].HasPassphrase);
    }

    [Fact]
    public async Task EmptyPassphraseLeavesTheStoredOneUnchanged()
    {
        var vm = await LoadedAsync(Entry("A", hasPassphrase: true));
        vm.Selected!.Name = "Renamed";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.DoesNotContain(_vault.Calls, c => c.StartsWith("SetAsync", StringComparison.Ordinal));
        Assert.True(vm.Selected.HasPassphrase);
    }

    [Fact]
    public async Task RemoveDeletesTheEntryAndItsVaultSecretOnSave()
    {
        var entry = Entry("A", hasPassphrase: true);
        _dialogs.DeleteAnswer = true;
        var vm = await LoadedAsync(entry, Entry("B"));

        await vm.RemoveCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(["A"], _dialogs.DeletePrompts);
        Assert.Equal(["B"], (await _store.LoadAsync(CancellationToken.None)).Select(c => c.Name));
        Assert.Contains($"RemoveOwnerAsync:Certificate:{entry.Id}", _vault.Calls);
    }

    [Fact]
    public async Task DeclinedRemovalKeepsTheEntry()
    {
        var vm = await LoadedAsync(Entry("A"));

        await vm.RemoveCommand.ExecuteAsync(null);

        Assert.Single(vm.Items);
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task OrderButtonsMoveTheSelectedEntry()
    {
        var vm = await LoadedAsync(Entry("A"), Entry("B"), Entry("C"));
        Assert.False(vm.MoveUpCommand.CanExecute(null));

        vm.Selected = vm.Items[1];
        vm.MoveUpCommand.Execute(null);
        Assert.Equal(["B", "A", "C"], vm.Items.Select(i => i.Name));
        Assert.False(vm.MoveUpCommand.CanExecute(null));

        vm.MoveDownCommand.Execute(null);
        vm.MoveDownCommand.Execute(null);
        Assert.Equal(["A", "C", "B"], vm.Items.Select(i => i.Name));
        Assert.False(vm.MoveDownCommand.CanExecute(null));

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(["A", "C", "B"], (await _store.LoadAsync(CancellationToken.None)).Select(c => c.Name));
    }

    [Fact]
    public async Task BrowseCommandsSetThePaths()
    {
        var vm = await LoadedAsync(Entry("A"));
        _dialogs.PickFileAnswer = "k.pem";
        await vm.BrowseKeyCommand.ExecuteAsync(null);
        _dialogs.PickFileAnswer = "c.pem";
        await vm.BrowseCertificateCommand.ExecuteAsync(null);
        _dialogs.PickFileAnswer = null;
        await vm.BrowseCertificateCommand.ExecuteAsync(null);

        Assert.Equal(("c.pem", "k.pem"), (vm.Selected!.CertificatePath, vm.Selected.KeyPath));
    }

    [Fact]
    public async Task DirtyTrackingFollowsEditsSaveAndLoad()
    {
        var vm = await LoadedAsync(Entry("A"));
        Assert.False(vm.IsDirty);

        vm.Selected!.HostPattern = "*.example.com";
        Assert.True(vm.IsDirty);

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.IsDirty);

        vm.Selected.Name = "B";
        await vm.LoadAsync();
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public async Task KeyPathIsVisibleForPemOnly()
    {
        var vm = await LoadedAsync(Entry("A"));
        Assert.False(vm.Selected!.IsKeyPathVisible);

        vm.Selected.Format = ClientCertificateFormat.Pem;

        Assert.True(vm.Selected.IsKeyPathVisible);
    }

    [Fact]
    public async Task VaultStateIsExposedAndNotified()
    {
        var vm = Create();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.Equal(VaultState.Unlocked, vm.VaultState);

        _vault.Lock();
        await Task.Yield();

        Assert.Contains(nameof(CertificatesViewModel.VaultState), changes);
    }
}
