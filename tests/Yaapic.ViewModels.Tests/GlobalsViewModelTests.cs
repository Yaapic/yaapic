using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Environments;

namespace Yaapic.ViewModels.Tests;

public sealed class GlobalsViewModelTests
{
    private readonly InMemoryGlobalsStore _store = new();
    private readonly GlobalsViewModel _vm;

    public GlobalsViewModelTests() => _vm = new GlobalsViewModel(_store);

    [Fact]
    public async Task LoadShowsStoredVariablesWithoutMarkingDirty()
    {
        await _store.SaveAsync([new KeyValueEntry("host", "a")], CancellationToken.None);

        await _vm.LoadAsync();

        Assert.False(_vm.IsDirty);
        Assert.Equal("host", Assert.Single(_vm.Current).Key);
    }

    [Fact]
    public async Task EditMarksDirtyAndRevertCleans()
    {
        await _store.SaveAsync([new KeyValueEntry("host", "a")], CancellationToken.None);
        await _vm.LoadAsync();

        _vm.Variables.Rows[0].Value = "b";
        Assert.True(_vm.IsDirty);
        Assert.Equal("b", Assert.Single(_vm.Current).Value);

        _vm.Variables.Rows[0].Value = "a";
        Assert.False(_vm.IsDirty);
    }

    [Fact]
    public async Task SaveWritesTheStoreAndClearsDirty()
    {
        await _vm.LoadAsync();
        _vm.Variables.Rows[0].Key = "k";
        Assert.True(_vm.IsDirty);

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.False(_vm.IsDirty);
        Assert.Equal("k", Assert.Single(await _store.LoadAsync(CancellationToken.None)).Key);
    }

    [Fact]
    public async Task LoadFailureSetsErrorAndDisablesSave()
    {
        _store.FailureMessage = "corrupt";

        await _vm.LoadAsync();

        Assert.Equal("corrupt", _vm.LastError);
        Assert.False(_vm.SaveCommand.CanExecute(null));
        Assert.Empty(_vm.Current);
        Assert.Equal(0, _store.SaveCount);
    }

    [Fact]
    public async Task SuccessfulReloadReenablesSave()
    {
        _store.FailureMessage = "corrupt";
        await _vm.LoadAsync();
        _store.FailureMessage = null;

        await _vm.LoadAsync();

        Assert.Null(_vm.LastError);
        Assert.True(_vm.SaveCommand.CanExecute(null));
    }
}
