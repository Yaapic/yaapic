using Yaapic.Core.Http;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class VariableScopeProviderApplyTests
{
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _collectionStore;
    private readonly InMemoryEnvironmentStore _environmentStore = new();
    private readonly InMemoryGlobalsStore _globalsStore = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeHttpTransport _transport = new((_, _) => Task.FromResult(SendResult.Success(
        new HttpResponseData(200, "OK", [], [], TimeSpan.FromMilliseconds(1), "https://x.test"))));
    private readonly EnvironmentsViewModel _environments;
    private readonly GlobalsViewModel _globals;
    private readonly CollectionTreeViewModel _tree;
    private readonly VariableScopeProvider _provider;
    private readonly RequestSender _sender;

    public VariableScopeProviderApplyTests()
    {
        _collectionStore = new InMemoryCollectionStore(_requests);
        _sender = new RequestSender(_transport);
        _environments = new EnvironmentsViewModel(_environmentStore, _dialogs);
        _globals = new GlobalsViewModel(_globalsStore);
        IVariableScopeProvider? scopes = null;
        var tabs = new Yaapic.ViewModels.Tabs.TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, _sender, scopes!, isNew));
        _tree = new CollectionTreeViewModel(_collectionStore, _requests, tabs, _dialogs);
        _provider = new VariableScopeProvider(_environments, _globals, _tree);
        scopes = _provider;
    }

    private async Task<HttpRequestDefinition> SeedRequestAsync(params KeyValueEntry[] collectionVariables)
    {
        var folder = FolderDefinition.Create("c") with { Variables = collectionVariables };
        await _collectionStore.SaveFolderAsync(folder, null, CancellationToken.None);
        var request = HttpRequestDefinition.Create("r");
        await _requests.SaveAsync(request, CancellationToken.None);
        await _collectionStore.MoveAsync(request.Id, folder.Id, CancellationToken.None);
        await _tree.RefreshAsync();
        _collectionStore.ResetSaveCount();
        return request;
    }

    private async Task ActivateEnvironmentAsync(params KeyValueEntry[] variables)
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "dev", variables);
        await _environmentStore.SaveAsync(environment, CancellationToken.None);
        await _environments.LoadAsync(environment.Id);
        _environmentStore.ResetSaveCount();
    }

    private static VariableChange Set(VariableScopeKind scope, string name, string value) => new(scope, name, value);

    private static VariableChange Unset(VariableScopeKind scope, string name) => new(scope, name, null);

    [Fact]
    public async Task SetThenReadBackThroughGetScopesForEachScope()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync();

        var errors = await _provider.ApplyChangesAsync(request.Id,
        [
            Set(VariableScopeKind.Environment, "e", "1"),
            Set(VariableScopeKind.Collection, "c", "2"),
            Set(VariableScopeKind.Globals, "g", "3"),
        ]);

        Assert.Empty(errors);
        var scopes = _provider.GetScopes(request.Id);
        Assert.Equal("1", Assert.Single(scopes.Environment!.Variables).Value);
        Assert.Equal("2", Assert.Single(scopes.Collection).Value);
        Assert.Equal("3", Assert.Single(scopes.Globals).Value);
    }

    [Fact]
    public async Task UnsetRemovesTheRow()
    {
        var request = await SeedRequestAsync(new KeyValueEntry("c", "1"), new KeyValueEntry("keep", "k"));
        await ActivateEnvironmentAsync(new KeyValueEntry("e", "1"));

        await _provider.ApplyChangesAsync(request.Id,
            [Unset(VariableScopeKind.Environment, "e"), Unset(VariableScopeKind.Collection, "c")]);

        var scopes = _provider.GetScopes(request.Id);
        Assert.Empty(scopes.Environment!.Variables);
        Assert.Equal("keep", Assert.Single(scopes.Collection).Key);
    }

    [Fact]
    public async Task UnsetVariableAsyncRemovesTheRowAndIgnoresAnAbsentOne()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync(new KeyValueEntry("e", "1"));

        Assert.Null(await _provider.UnsetVariableAsync(request.Id, VariableScopeKind.Environment, "absent"));
        Assert.Null(await _provider.UnsetVariableAsync(request.Id, VariableScopeKind.Environment, "e"));
        Assert.Empty(_provider.GetScopes(request.Id).Environment!.Variables);
        Assert.NotNull(await _provider.UnsetVariableAsync(request.Id, VariableScopeKind.Environment, " "));
    }

    [Fact]
    public async Task FiveEnvironmentChangesSaveOnce()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync(new KeyValueEntry("old", "x"));

        await _provider.ApplyChangesAsync(request.Id,
        [
            Set(VariableScopeKind.Environment, "a", "1"),
            Set(VariableScopeKind.Environment, "b", "2"),
            Set(VariableScopeKind.Environment, "a", "3"),
            Unset(VariableScopeKind.Environment, "old"),
            Set(VariableScopeKind.Environment, "c", "4"),
        ]);

        Assert.Equal(1, _environmentStore.SaveCount);
        var variables = _provider.GetScopes(request.Id).Environment!.Variables;
        Assert.Equal(["a=3", "b=2", "c=4"], variables.Select(v => $"{v.Key}={v.Value}"));
    }

    [Fact]
    public async Task MixedScopesSaveOnceEach()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync();

        await _provider.ApplyChangesAsync(request.Id,
        [
            Set(VariableScopeKind.Environment, "e1", "1"),
            Set(VariableScopeKind.Collection, "c1", "1"),
            Set(VariableScopeKind.Globals, "g1", "1"),
            Set(VariableScopeKind.Environment, "e2", "2"),
            Set(VariableScopeKind.Collection, "c2", "2"),
            Set(VariableScopeKind.Globals, "g2", "2"),
        ]);

        Assert.Equal(1, _environmentStore.SaveCount);
        Assert.Equal(1, _collectionStore.SaveFolderCount);
        Assert.Equal(1, _globalsStore.SaveCount);
    }

    [Fact]
    public async Task ANonWritableScopeIsSkippedSilently()
    {
        var request = await SeedRequestAsync();

        var errors = await _provider.ApplyChangesAsync(request.Id, [Set(VariableScopeKind.Environment, "e", "1")]);

        Assert.Empty(errors);
        Assert.Equal(0, _environmentStore.SaveCount);
        Assert.Null(_provider.GetScopes(request.Id).Environment);
    }

    [Fact]
    public async Task AStoreFailureReportsThatScopeOnlyAndTheOthersStillSave()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync();
        _environmentStore.FailureMessage = "disk full";

        var errors = await _provider.ApplyChangesAsync(request.Id,
        [
            Set(VariableScopeKind.Environment, "e", "1"),
            Set(VariableScopeKind.Collection, "c", "2"),
            Set(VariableScopeKind.Globals, "g", "3"),
        ]);

        Assert.Equal(["disk full"], errors);
        var scopes = _provider.GetScopes(request.Id);
        Assert.Equal("2", Assert.Single(scopes.Collection).Value);
        Assert.Equal("3", Assert.Single(scopes.Globals).Value);
    }

    [Fact]
    public async Task AnExistingSecretRowKeepsIsSecretAndANewRowIsNotSecret()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync(new KeyValueEntry("token", "old", true, true));

        await _provider.ApplyChangesAsync(request.Id,
            [Set(VariableScopeKind.Environment, "token", "new"), Set(VariableScopeKind.Environment, "fresh", "v")]);

        var variables = _provider.GetScopes(request.Id).Environment!.Variables;
        var token = Assert.Single(variables, v => v.Key == "token");
        Assert.Equal("new", token.Value);
        Assert.True(token.IsSecret);
        Assert.False(Assert.Single(variables, v => v.Key == "fresh").IsSecret);
    }

    [Fact]
    public async Task ScopesChangedIsRaisedExactlyOncePerApplyChanges()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync();
        var raised = 0;
        _provider.ScopesChanged += (_, _) => raised++;

        await _provider.ApplyChangesAsync(request.Id,
        [
            Set(VariableScopeKind.Environment, "e1", "1"),
            Set(VariableScopeKind.Environment, "e2", "2"),
            Set(VariableScopeKind.Collection, "c", "3"),
            Set(VariableScopeKind.Globals, "g", "4"),
        ]);

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task APreRequestScriptVariableIsResolvedByTheNextTokenize()
    {
        var request = await SeedRequestAsync();
        await ActivateEnvironmentAsync();
        ScriptEngineRegistry registry = new();
        registry.Add(new FakeScriptEngine(i =>
        {
            i.Host.SetVariable(ScriptScope.Environment, "token", "abc");
            return new ScriptRunResult(i.Phase, i.Origin, i.Block.Language, ScriptRunStatus.Succeeded, null, [], [], TimeSpan.Zero);
        }));
        var services = new ScriptSendServices(
            new ScriptedRequestSender(_sender, registry, TimeProvider.System),
            new FakeScriptChainProvider(),
            new ConsoleViewModel(),
            _provider);
        var editor = RequestEditorViewModel.FromDefinition(request, _sender, _provider, scripting: services);
        editor.Url = "https://x.test/{{token}}";
        editor.Scripts.PreRequest.Source = "x();";
        Assert.False(Assert.Single(editor.Variables.Tokenize(editor.Url)).Hint.IsResolved);

        await editor.SendCommand.ExecuteAsync(null);

        Assert.True(Assert.Single(editor.Variables.Tokenize(editor.Url)).Hint.IsResolved);
        Assert.Equal("abc", Assert.Single(_provider.GetScopes(request.Id).Environment!.Variables).Value);
    }
}
