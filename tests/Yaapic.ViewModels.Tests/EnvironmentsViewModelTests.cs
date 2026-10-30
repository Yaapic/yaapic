using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class EnvironmentsViewModelTests
{
    private readonly InMemoryEnvironmentStore _store = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly EnvironmentsViewModel _vm;

    public EnvironmentsViewModelTests() => _vm = new EnvironmentsViewModel(_store, _dialogs);

    private async Task<EnvironmentDefinition> SeedAsync(string name, params KeyValueEntry[] variables)
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), name, variables);
        await _store.SaveAsync(environment, CancellationToken.None);
        return environment;
    }

    [Fact]
    public async Task LoadSelectsTheGivenActiveId()
    {
        await SeedAsync("a");
        var b = await SeedAsync("b");

        await _vm.LoadAsync(b.Id);

        Assert.Equal(2, _vm.Environments.Count);
        Assert.Equal(b.Id, _vm.ActiveEnvironment!.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoadSelectsNothingForNullOrUnknownId(bool unknown)
    {
        await SeedAsync("a");

        await _vm.LoadAsync(unknown ? Guid.NewGuid() : null);

        Assert.Null(_vm.ActiveEnvironment);
        Assert.Null(_vm.Current);
    }

    [Fact]
    public async Task CurrentReflectsUnsavedVariableEdits()
    {
        var a = await SeedAsync("a", new KeyValueEntry("host", "old"));
        await _vm.LoadAsync(a.Id);

        _vm.ActiveEnvironment!.Variables.Rows[0].Value = "new";

        Assert.True(_vm.ActiveEnvironment.IsDirty);
        Assert.Equal("new", Assert.Single(_vm.Current!.Variables).Value);
        Assert.Equal("old", Assert.Single(await _store.ListAsync(CancellationToken.None)).Variables[0].Value);
    }

    [Fact]
    public void AddCreatesADirtyEnvironmentForEditingWithoutActivatingIt()
    {
        _vm.AddCommand.Execute(null);

        var item = Assert.Single(_vm.Environments);
        Assert.Equal("New Environment", item.Name);
        Assert.True(item.IsDirty);
        Assert.Same(item, _vm.EditedEnvironment);
        Assert.Null(_vm.ActiveEnvironment);
    }

    [Fact]
    public async Task SaveStoresAndMarksClean()
    {
        _vm.AddCommand.Execute(null);
        var item = _vm.EditedEnvironment!;
        item.Name = "dev";

        await _vm.SaveCommand.ExecuteAsync(item);

        Assert.False(item.IsDirty);
        Assert.Equal("dev", Assert.Single(await _store.ListAsync(CancellationToken.None)).Name);
        item.Name = "prod";
        Assert.True(item.IsDirty);
    }

    [Fact]
    public async Task ConfirmedDeleteRemovesFromStoreAndClearsActiveAndEdited()
    {
        var a = await SeedAsync("a");
        await _vm.LoadAsync(a.Id);
        var item = _vm.ActiveEnvironment!;
        _vm.EditedEnvironment = item;
        _dialogs.DeleteAnswer = true;

        await _vm.DeleteCommand.ExecuteAsync(item);

        Assert.Equal(["a"], _dialogs.DeletePrompts);
        Assert.Empty(_vm.Environments);
        Assert.Empty(await _store.ListAsync(CancellationToken.None));
        Assert.Null(_vm.ActiveEnvironment);
        Assert.Null(_vm.EditedEnvironment);
    }

    [Fact]
    public async Task RefusedDeleteChangesNothing()
    {
        var a = await SeedAsync("a");
        await _vm.LoadAsync(a.Id);

        await _vm.DeleteCommand.ExecuteAsync(_vm.ActiveEnvironment);

        Assert.Single(_vm.Environments);
        Assert.Single(await _store.ListAsync(CancellationToken.None));
        Assert.NotNull(_vm.ActiveEnvironment);
    }

    [Fact]
    public async Task DeletingAnotherEnvironmentKeepsTheActiveOne()
    {
        await SeedAsync("a");
        var b = await SeedAsync("b");
        await _vm.LoadAsync(b.Id);

        await _vm.DeleteConfirmedAsync(_vm.Environments[0]);

        Assert.Equal(b.Id, _vm.ActiveEnvironment!.Id);
    }

    [Fact]
    public async Task ActiveEnvironmentChangedIsRaised()
    {
        await SeedAsync("a");
        await _vm.LoadAsync(null);
        var raised = 0;
        _vm.ActiveEnvironmentChanged += (_, _) => raised++;

        _vm.ActiveEnvironment = _vm.Environments[0];
        _vm.ActiveEnvironment = null;

        Assert.Equal(2, raised);
    }

    [Fact]
    public async Task RequestEditorSendsWithTheActiveEnvironmentVariable()
    {
        var a = await SeedAsync("a", new KeyValueEntry("host", "old.test"));
        await _vm.LoadAsync(a.Id);
        _vm.ActiveEnvironment!.Variables.Rows[0].Value = "api.test";
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(
            new HttpResponseData(200, "OK", [], [], TimeSpan.FromMilliseconds(1), "https://api.test"))));
        var editor = RequestEditorViewModel.CreateNew(new RequestSender(transport), _vm);
        editor.Url = "https://{{host}}/users";

        await editor.SendCommand.ExecuteAsync(null);

        Assert.Equal("https://api.test/users", Assert.Single(transport.Requests).Url);
    }
}
