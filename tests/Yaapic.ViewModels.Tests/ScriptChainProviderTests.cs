using Yaapic.Core.Scripting;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;

namespace Yaapic.ViewModels.Tests;

public sealed class ScriptChainProviderTests
{
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _store;
    private readonly CollectionTreeViewModel _tree;
    private readonly CollectionTreeScriptChainProvider _provider;

    public ScriptChainProviderTests()
    {
        _store = new InMemoryCollectionStore(_requests);
        var dialogs = new FakeDialogService();
        var tabs = new Yaapic.ViewModels.Tabs.TabsViewModel(_requests, dialogs, (_, _) => throw new NotSupportedException());
        _tree = new CollectionTreeViewModel(_store, _requests, tabs, dialogs);
        _provider = new CollectionTreeScriptChainProvider(_tree);
    }

    private static ScriptSet Pre(string source) => ScriptSet.Empty.With(ScriptPhase.PreRequest, new ScriptBlock(ScriptLanguage.JavaScript, source));

    private async Task<FolderDefinition> AddFolderAsync(string name, Guid? parent, string source = "")
    {
        var folder = FolderDefinition.Create(name) with { Scripts = Pre(source) };
        await _store.SaveFolderAsync(folder, parent, CancellationToken.None);
        return folder;
    }

    private async Task<HttpRequestDefinition> AddRequestAsync(Guid? folder)
    {
        var request = HttpRequestDefinition.Create("r");
        await _requests.SaveAsync(request, CancellationToken.None);
        if (folder is { } id) await _store.MoveAsync(request.Id, id, CancellationToken.None);
        await _tree.RefreshAsync();
        return request;
    }

    [Fact]
    public async Task UnfiledRequestHasNoAncestors()
    {
        var request = await AddRequestAsync(null);
        Assert.Empty(_provider.GetAncestors(request.Id).Steps);
    }

    [Fact]
    public async Task UnknownIdHasNoAncestors()
    {
        await AddFolderAsync("c", null);
        await _tree.RefreshAsync();
        Assert.Empty(_provider.GetAncestors(Guid.NewGuid()).Steps);
    }

    [Fact]
    public async Task RequestInCollectionGivesTheCollection()
    {
        var c = await AddFolderAsync("c", null, "a");
        var request = await AddRequestAsync(c.Id);
        var steps = _provider.GetAncestors(request.Id).Steps;
        var step = Assert.Single(steps);
        Assert.Equal("c", step.Origin);
        Assert.Equal("a", step.Scripts.PreRequest.Source);
    }

    [Fact]
    public async Task NestedRequestGivesFoldersOutermostFirst()
    {
        var c = await AddFolderAsync("c", null);
        var f = await AddFolderAsync("f", c.Id);
        var sub = await AddFolderAsync("sub", f.Id);
        await AddFolderAsync("other", c.Id);
        var request = await AddRequestAsync(sub.Id);
        Assert.Equal(["c", "f", "sub"], _provider.GetAncestors(request.Id).Steps.Select(s => s.Origin));
    }

    [Fact]
    public async Task FolderEditIsReflectedAfterRefresh()
    {
        var c = await AddFolderAsync("c", null, "old");
        var request = await AddRequestAsync(c.Id);
        await _store.SaveFolderAsync(c with { Name = "renamed", Scripts = Pre("new") }, null, CancellationToken.None);
        await _tree.RefreshAsync();
        var step = Assert.Single(_provider.GetAncestors(request.Id).Steps);
        Assert.Equal("renamed", step.Origin);
        Assert.Equal("new", step.Scripts.PreRequest.Source);
    }

    [Fact]
    public void WithRequestAppendsLast()
    {
        var chain = new ScriptChain([new ScriptStep("c", ScriptSet.Empty)]).WithRequest("req", Pre("x"));
        Assert.Equal(["c", "req"], chain.Steps.Select(s => s.Origin));
        Assert.Empty(ScriptChain.Empty.Steps);
    }
}
