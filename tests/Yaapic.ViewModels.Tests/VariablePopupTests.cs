using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class VariablePopupTests
{
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _collectionStore;
    private readonly InMemoryEnvironmentStore _environmentStore = new();
    private readonly InMemoryGlobalsStore _globalsStore = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly EnvironmentsViewModel _environments;
    private readonly GlobalsViewModel _globals;
    private readonly CollectionTreeViewModel _tree;
    private readonly VariableScopeProvider _provider;

    public VariablePopupTests()
    {
        _collectionStore = new InMemoryCollectionStore(_requests);
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new InvalidOperationException()));
        _environments = new EnvironmentsViewModel(_environmentStore, _dialogs);
        _globals = new GlobalsViewModel(_globalsStore);
        IVariableScopeProvider? scopes = null;
        var tabs = new Yaapic.ViewModels.Tabs.TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, scopes!, isNew));
        _tree = new CollectionTreeViewModel(_collectionStore, _requests, tabs, _dialogs);
        _provider = new VariableScopeProvider(_environments, _globals, _tree);
        scopes = _provider;
    }

    private async Task<Guid> SeedRequestAsync(bool inCollection = true, params KeyValueEntry[] collectionVariables)
    {
        var request = HttpRequestDefinition.Create("r");
        await _requests.SaveAsync(request, CancellationToken.None);
        if (inCollection)
        {
            var folder = FolderDefinition.Create("c") with { Variables = collectionVariables };
            await _collectionStore.SaveFolderAsync(folder, null, CancellationToken.None);
            await _collectionStore.MoveAsync(request.Id, folder.Id, CancellationToken.None);
        }

        await _tree.RefreshAsync();
        return request.Id;
    }

    private async Task<EnvironmentDefinition> ActivateEnvironmentAsync(params KeyValueEntry[] variables)
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "dev", variables);
        await _environmentStore.SaveAsync(environment, CancellationToken.None);
        await _environments.LoadAsync(environment.Id);
        return environment;
    }

    private VariableContext ContextFor(Guid requestId) => new(requestId, _provider);

    [Fact]
    public async Task UnresolvedPopupDefaultsToTheFirstEnabledScope()
    {
        var id = await SeedRequestAsync();
        await ActivateEnvironmentAsync();

        var popup = ContextFor(id).CreatePopup("nope");

        Assert.False(popup.IsResolved);
        Assert.Equal(VariableScopeKind.Environment, popup.SelectedScope);
        Assert.Equal("Unresolved variable", popup.Title);
        Assert.Equal("This variable is not defined in the active collection, environment or globals.", popup.Message);
        Assert.Equal("Create", popup.SubmitLabel);
        Assert.True(popup.IsEditing);
        Assert.True(popup.CanSubmit);
        Assert.Equal(
            [VariableScopeKind.Environment, VariableScopeKind.Collection, VariableScopeKind.Globals],
            popup.ScopeChoices.Select(c => c.Kind));
    }

    [Fact]
    public async Task ChoicesAreDisabledWithAReason()
    {
        var id = await SeedRequestAsync(inCollection: false);

        var popup = ContextFor(id).CreatePopup("nope");

        Assert.Equal("No environment is active", popup.ScopeChoices[0].DisabledReason);
        Assert.Equal("Request is not in a collection", popup.ScopeChoices[1].DisabledReason);
        Assert.True(popup.ScopeChoices[2].IsEnabled);
        Assert.Equal(VariableScopeKind.Globals, popup.SelectedScope);
    }

    [Fact]
    public async Task ResolvedPopupShowsTheValueAndEditsOnDemand()
    {
        var id = await SeedRequestAsync(true, new KeyValueEntry("v", "1"));

        var popup = ContextFor(id).CreatePopup("v");

        Assert.True(popup.IsResolved);
        Assert.Equal("v", popup.Title);
        Assert.Equal("", popup.Message);
        Assert.Equal("1", popup.Value);
        Assert.Equal("Collection", popup.ScopeLabel);
        Assert.Equal(VariableScopeKind.Collection, popup.SelectedScope);
        Assert.Equal("Save", popup.SubmitLabel);
        Assert.False(popup.IsEditing);
        popup.ToggleEditCommand.Execute(null);
        Assert.True(popup.IsEditing);
        Assert.Equal("1", popup.EditableValue);
    }

    [Fact]
    public async Task DynamicPopupIsNeverEditable()
    {
        var id = await SeedRequestAsync();

        var popup = ContextFor(id).CreatePopup("$guid");

        Assert.True(popup.IsDynamic);
        Assert.Null(popup.Value);
        Assert.Equal("Changes every time the request is sent.", popup.Message);
        Assert.False(popup.CanEdit);
        Assert.False(popup.CanSubmit);
        popup.ToggleEditCommand.Execute(null);
        Assert.False(popup.IsEditing);
    }

    [Theory]
    [InlineData(VariableScopeKind.Environment)]
    [InlineData(VariableScopeKind.Collection)]
    [InlineData(VariableScopeKind.Globals)]
    public async Task CreatingWritesTheStoreAndResolvesInThatScope(VariableScopeKind scope)
    {
        var id = await SeedRequestAsync();
        var environment = await ActivateEnvironmentAsync();
        var context = ContextFor(id);
        context.IsActive = true;
        var changed = 0;
        context.Changed += (_, _) => changed++;
        var popup = context.CreatePopup("token");
        popup.SelectedScope = scope;
        popup.EditableValue = "abc";

        await popup.SubmitCommand.ExecuteAsync(null);

        Assert.Null(popup.LastError);
        Assert.True(changed > 0);
        var hint = context.Tokenize("{{token}}")[0].Hint;
        Assert.True(hint.IsResolved);
        Assert.Equal("abc", hint.Value);
        Assert.Equal(scope, hint.Scope);
        var stored = scope switch
        {
            VariableScopeKind.Environment => (await _environmentStore.ListAsync(CancellationToken.None)).Single(e => e.Id == environment.Id).Variables,
            VariableScopeKind.Collection => (await _collectionStore.LoadTreeAsync(CancellationToken.None)).Single().Folder!.Variables,
            _ => await _globalsStore.LoadAsync(CancellationToken.None),
        };
        Assert.Equal([new KeyValueEntry("token", "abc")], stored);
    }

    [Fact]
    public async Task UpdatingAnExistingRowDoesNotDuplicateItAndEnablesIt()
    {
        var id = await SeedRequestAsync(true, new KeyValueEntry("a", "1"), new KeyValueEntry("v", "old", false));
        var context = ContextFor(id);

        var error = await _provider.SetVariableAsync(id, VariableScopeKind.Collection, "v", "new");

        Assert.Null(error);
        Assert.Equal(
            [new KeyValueEntry("a", "1"), new KeyValueEntry("v", "new")],
            (await _collectionStore.LoadTreeAsync(CancellationToken.None)).Single().Folder!.Variables);
        Assert.Equal("new", context.Describe("v").Value);
    }

    [Fact]
    public async Task StoreFailureKeepsTheFormOpenWithTheError()
    {
        var id = await SeedRequestAsync();
        await ActivateEnvironmentAsync();
        _environmentStore.FailureMessage = "disk full";
        var popup = ContextFor(id).CreatePopup("x");
        var submitted = false;
        popup.Submitted += (_, _) => submitted = true;

        await popup.SubmitCommand.ExecuteAsync(null);

        Assert.Equal("disk full", popup.LastError);
        Assert.False(submitted);
        Assert.True(popup.IsEditing);
        Assert.False(popup.IsBusy);
    }

    [Fact]
    public async Task CorruptGlobalsReturnTheLoadError()
    {
        _globalsStore.FailureMessage = "corrupt";
        await _globals.LoadAsync();
        var id = await SeedRequestAsync(inCollection: false);

        Assert.Equal("corrupt", await _provider.SetVariableAsync(id, VariableScopeKind.Globals, "a", "1"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("$guid")]
    [InlineData("a{b")]
    public async Task InvalidNamesAreRejectedWithoutWriting(string name)
    {
        var id = await SeedRequestAsync();

        Assert.NotNull(await _provider.SetVariableAsync(id, VariableScopeKind.Globals, name, "v"));
        Assert.Equal(0, _globalsStore.SaveCount);
    }

    [Fact]
    public async Task ScopesOutsideTheWritableListAreRejected()
    {
        var id = await SeedRequestAsync(inCollection: false);

        Assert.NotNull(await _provider.SetVariableAsync(id, VariableScopeKind.Collection, "a", "1"));
        Assert.NotNull(await _provider.SetVariableAsync(id, VariableScopeKind.Environment, "a", "1"));
        Assert.NotNull(await _provider.SetVariableAsync(id, VariableScopeKind.Dynamic, "a", "1"));
        Assert.Equal(0, _globalsStore.SaveCount);
    }

    [Fact]
    public async Task OpenRaisesTheRequestForTheVariablesScope()
    {
        var id = await SeedRequestAsync(true, new KeyValueEntry("v", "1"));
        var context = ContextFor(id);
        VariableScopeKind? opened = null;
        context.OpenRequested += (_, scope) => opened = scope;

        context.CreatePopup("v").OpenCommand.Execute(null);

        Assert.Equal(VariableScopeKind.Collection, opened);
    }

    [Fact]
    public async Task EditingTheActiveEnvironmentRaisesScopesChangedButAnInactiveOneDoesNot()
    {
        var active = await ActivateEnvironmentAsync();
        var other = new EnvironmentDefinition(Guid.NewGuid(), "other", []);
        await _environmentStore.SaveAsync(other, CancellationToken.None);
        await _environments.LoadAsync(active.Id);
        var raised = 0;
        _provider.ScopesChanged += (_, _) => raised++;

        _environments.Environments.Single(e => e.Id == active.Id).Variables.Rows[^1].Key = "k";
        Assert.Equal(1, raised);

        _environments.Environments.Single(e => e.Id == other.Id).Variables.Rows[^1].Key = "k";
        Assert.Equal(1, raised);

        _environments.ActiveEnvironment = _environments.Environments.Single(e => e.Id == other.Id);
        Assert.Equal(2, raised);
        _environments.Environments.Single(e => e.Id == active.Id).Variables.Rows[^1].Key = "z";
        Assert.Equal(2, raised);
        _environments.ActiveEnvironment!.Variables.Rows[^1].Key = "k";
        Assert.Equal(3, raised);
    }
}
