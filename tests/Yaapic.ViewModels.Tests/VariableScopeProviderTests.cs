using Yaapic.Core.Http;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class VariableScopeProviderTests
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

    public VariableScopeProviderTests()
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

    private async Task<HttpRequestDefinition> SeedRequestAsync(string url, params KeyValueEntry[] collectionVariables)
    {
        var folder = FolderDefinition.Create("c") with { Variables = collectionVariables };
        await _collectionStore.SaveFolderAsync(folder, null, CancellationToken.None);
        var request = HttpRequestDefinition.Create("r") with { Url = url };
        await _requests.SaveAsync(request, CancellationToken.None);
        await _collectionStore.MoveAsync(request.Id, folder.Id, CancellationToken.None);
        await _tree.RefreshAsync();
        return request;
    }

    private async Task ActivateEnvironmentAsync(params KeyValueEntry[] variables)
    {
        var environment = new EnvironmentDefinition(Guid.NewGuid(), "dev", variables);
        await _environmentStore.SaveAsync(environment, CancellationToken.None);
        await _environments.LoadAsync(environment.Id);
    }

    private void SetGlobal(string key, string value)
    {
        var row = _globals.Variables.Rows[0];
        row.Key = key;
        row.Value = value;
    }

    private async Task<string> SendAsync(HttpRequestDefinition request)
    {
        var editor = RequestEditorViewModel.FromDefinition(request, _sender, _provider);
        await editor.SendCommand.ExecuteAsync(null);
        return _transport.Requests[^1].Url;
    }

    [Fact]
    public async Task CombinesTheThreeScopesForARequestInACollection()
    {
        var request = await SeedRequestAsync("x", new KeyValueEntry("c", "1"));
        await ActivateEnvironmentAsync(new KeyValueEntry("e", "2"));
        SetGlobal("g", "3");

        var scopes = _provider.GetScopes(request.Id);

        Assert.Equal("dev", scopes.Environment!.Name);
        Assert.Equal("c", Assert.Single(scopes.Collection).Key);
        Assert.Equal("g", Assert.Single(scopes.Globals).Key);
    }

    [Fact]
    public async Task AnUnfiledRequestHasNoCollectionVariables()
    {
        await SeedRequestAsync("x", new KeyValueEntry("c", "1"));
        var unfiled = HttpRequestDefinition.Create("u");
        await _requests.SaveAsync(unfiled, CancellationToken.None);
        await _tree.RefreshAsync();

        Assert.Empty(_provider.GetScopes(unfiled.Id).Collection);
    }

    [Fact]
    public async Task ScopesChangedFiresOnEnvironmentGlobalsAndTreeChanges()
    {
        var raised = 0;
        _provider.ScopesChanged += (_, _) => raised++;

        await ActivateEnvironmentAsync();
        Assert.True(raised > 0);

        raised = 0;
        SetGlobal("g", "3");
        Assert.True(raised > 0);

        raised = 0;
        await _tree.RefreshAsync();
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task SendingUsesCollectionAndGlobalsVariables()
    {
        var request = await SeedRequestAsync("https://{{host}}/{{token}}", new KeyValueEntry("host", "c.test"));
        SetGlobal("token", "tk");

        Assert.Equal("https://c.test/tk", await SendAsync(request));
    }

    [Fact]
    public async Task EnvironmentWinsOverCollectionWhichWinsOverGlobals()
    {
        var request = await SeedRequestAsync("https://{{a}}/{{b}}/{{c}}", new KeyValueEntry("a", "col"), new KeyValueEntry("b", "col"));
        await ActivateEnvironmentAsync(new KeyValueEntry("a", "env"));
        var rows = _globals.Variables.Rows;
        rows[0].Key = "a";
        rows[0].Value = "glob";
        rows[1].Key = "b";
        rows[1].Value = "glob";
        rows[2].Key = "c";
        rows[2].Value = "glob";

        Assert.Equal("https://env/col/glob", await SendAsync(request));
    }
}
