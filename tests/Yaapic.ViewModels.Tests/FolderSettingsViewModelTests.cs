using Yaapic.Core.Http;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class FolderSettingsViewModelTests
{
    private sealed class FailingStore : ICollectionStore
    {
        public Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct) => throw new StorageException("boom");

        public Task DeleteFolderAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();

        public Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct) => throw new NotSupportedException();
    }

    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _store;
    private readonly FakeDialogService _dialogs = new();
    private readonly CollectionTreeViewModel _tree;

    public FolderSettingsViewModelTests()
    {
        _store = new InMemoryCollectionStore(_requests);
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        var tabs = new TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider(), isNew));
        _tree = new CollectionTreeViewModel(_store, _requests, tabs, _dialogs);
    }

    private static FolderDefinition WithVariable(FolderDefinition folder) =>
        folder with { Variables = [new KeyValueEntry("host", "a", true)] };

    [Fact]
    public void EditsMakeItDirty()
    {
        var vm = new FolderSettingsViewModel(WithVariable(FolderDefinition.Create("c")), true, _store);
        Assert.False(vm.IsDirty);

        vm.Name = "d";
        Assert.True(vm.IsDirty);
        vm.Name = "c";
        Assert.False(vm.IsDirty);

        vm.Description = "x";
        Assert.True(vm.IsDirty);
        vm.Description = "";

        vm.Variables.Rows[0].Value = "b";
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task SaveWritesThroughAndRaisesSaved()
    {
        var folder = FolderDefinition.Create("c");
        await _store.SaveFolderAsync(folder, null, CancellationToken.None);
        await _tree.RefreshAsync();
        _tree.EditSettingsCommand.Execute(_tree.Roots[0]);
        var editor = _tree.SettingsEditor!;
        var saved = 0;
        editor.Saved += (_, _) => saved++;
        editor.Name = "  renamed ";
        editor.Variables.Rows[0].Key = "k";

        await editor.SaveCommand.ExecuteAsync(null);
        await _tree.RefreshAsync();

        Assert.Equal(1, saved);
        Assert.False(editor.IsDirty);
        Assert.Equal("renamed", _tree.Roots[0].Name);
        Assert.Equal("k", Assert.Single(_tree.Roots[0].Node.Folder!.Variables).Key);
    }

    [Fact]
    public async Task BlankNameKeepsTheOldOne()
    {
        var folder = FolderDefinition.Create("c");
        await _store.SaveFolderAsync(folder, null, CancellationToken.None);
        var vm = new FolderSettingsViewModel(folder, true, _store);
        vm.Name = "  ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("c", vm.Name);
        Assert.Equal("c", (await _store.LoadTreeAsync(CancellationToken.None))[0].Name);
    }

    [Fact]
    public async Task NestedFolderKeepsItsVariablesAndHidesThem()
    {
        var parent = FolderDefinition.Create("p");
        var child = WithVariable(FolderDefinition.Create("child"));
        await _store.SaveFolderAsync(parent, null, CancellationToken.None);
        await _store.SaveFolderAsync(child, parent.Id, CancellationToken.None);
        var vm = new FolderSettingsViewModel(child, false, _store);
        Assert.False(vm.ShowVariables);
        vm.Name = "renamed";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = CollectionNode.Flatten(await _store.LoadTreeAsync(CancellationToken.None)).Single(n => n.Id == child.Id);
        Assert.Equal("renamed", saved.Name);
        Assert.Equal("host", Assert.Single(saved.Folder!.Variables).Key);
    }

    [Fact]
    public async Task StorageFailureSetsLastError()
    {
        var vm = new FolderSettingsViewModel(FolderDefinition.Create("c"), true, new FailingStore());
        vm.Name = "d";
        var saved = false;
        vm.Saved += (_, _) => saved = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("boom", vm.LastError);
        Assert.False(saved);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task EditAndCloseSettingsSetAndClearTheEditor()
    {
        await _store.SaveFolderAsync(FolderDefinition.Create("c"), null, CancellationToken.None);
        var request = HttpRequestDefinition.Create("r");
        await _requests.SaveAsync(request, CancellationToken.None);
        await _tree.RefreshAsync();

        _tree.EditSettingsCommand.Execute(_tree.Roots.Single(n => !n.IsFolder));
        Assert.Null(_tree.SettingsEditor);

        _tree.EditSettingsCommand.Execute(_tree.Roots.Single(n => n.IsFolder));
        Assert.True(_tree.SettingsEditor!.ShowVariables);

        _tree.CloseSettingsCommand.Execute(null);
        Assert.Null(_tree.SettingsEditor);
    }

    [Fact]
    public async Task FindCollectionVariablesUsesTheTopLevelFolder()
    {
        var top = WithVariable(FolderDefinition.Create("top"));
        var inner = FolderDefinition.Create("inner");
        await _store.SaveFolderAsync(top, null, CancellationToken.None);
        await _store.SaveFolderAsync(inner, top.Id, CancellationToken.None);
        var nested = HttpRequestDefinition.Create("nested");
        var unfiled = HttpRequestDefinition.Create("unfiled");
        await _requests.SaveAsync(nested, CancellationToken.None);
        await _requests.SaveAsync(unfiled, CancellationToken.None);
        await _store.MoveAsync(nested.Id, inner.Id, CancellationToken.None);
        await _tree.RefreshAsync();

        Assert.Equal("host", Assert.Single(_tree.FindCollectionVariables(nested.Id)).Key);
        Assert.Empty(_tree.FindCollectionVariables(unfiled.Id));
        Assert.Empty(_tree.FindCollectionVariables(Guid.NewGuid()));
    }

    private static ScriptSet Pre(string source, ScriptLanguage language = ScriptLanguage.JavaScript) =>
        ScriptSet.Empty.With(ScriptPhase.PreRequest, new ScriptBlock(language, source));

    [Fact]
    public void ScriptEditsMakeItDirtyForSourceAndLanguage()
    {
        var vm = new FolderSettingsViewModel(FolderDefinition.Create("c") with { Scripts = Pre("a") }, false, _store);
        Assert.False(vm.IsDirty);

        vm.Scripts.PreRequest.Source = "b";
        Assert.True(vm.IsDirty);
        vm.Scripts.PreRequest.Source = "a";
        Assert.False(vm.IsDirty);

        vm.Scripts.PreRequest.Language = ScriptLanguage.Lua;
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public void ScriptsAreAlwaysShownAndHintFollowsTheLevel()
    {
        var folder = new FolderSettingsViewModel(FolderDefinition.Create("f"), false, _store);
        var collection = new FolderSettingsViewModel(FolderDefinition.Create("c"), true, _store);

        Assert.True(folder.ShowScripts);
        Assert.True(collection.ShowScripts);
        Assert.Equal("Runs for every request in this folder, before the request's own script.", folder.ScriptsHint);
        Assert.Equal("Runs for every request in this collection, before the folder and request scripts.", collection.ScriptsHint);
    }

    [Fact]
    public async Task SaveWritesTheScriptsAndTheStoreReloadsThem()
    {
        await _store.SaveFolderAsync(FolderDefinition.Create("c"), null, CancellationToken.None);
        await _tree.RefreshAsync();
        _tree.EditSettingsCommand.Execute(_tree.Roots[0]);
        var editor = _tree.SettingsEditor!;
        editor.Scripts.PreRequest.Language = ScriptLanguage.Lua;
        editor.Scripts.PreRequest.Source = "print(1)";

        await editor.SaveCommand.ExecuteAsync(null);
        await _tree.RefreshAsync();

        Assert.False(editor.IsDirty);
        var block = _tree.Roots[0].Node.Folder!.Scripts.PreRequest;
        Assert.Equal(ScriptLanguage.Lua, block.Language);
        Assert.Equal("print(1)", block.Source);
    }

    [Fact]
    public async Task NestedFolderScriptsDoNotAffectTheCollection()
    {
        var parent = FolderDefinition.Create("p") with { Scripts = Pre("outer") };
        var child = FolderDefinition.Create("child");
        await _store.SaveFolderAsync(parent, null, CancellationToken.None);
        await _store.SaveFolderAsync(child, parent.Id, CancellationToken.None);
        var vm = new FolderSettingsViewModel(child, false, _store);
        vm.Scripts.PreRequest.Source = "inner";

        await vm.SaveCommand.ExecuteAsync(null);

        var nodes = CollectionNode.Flatten(await _store.LoadTreeAsync(CancellationToken.None));
        Assert.Equal("outer", nodes.Single(n => n.Id == parent.Id).Folder!.Scripts.PreRequest.Source);
        Assert.Equal("inner", nodes.Single(n => n.Id == child.Id).Folder!.Scripts.PreRequest.Source);
    }

    [Fact]
    public async Task ClosingWithoutSavingDiscardsScriptEdits()
    {
        await _store.SaveFolderAsync(FolderDefinition.Create("c") with { Scripts = Pre("saved") }, null, CancellationToken.None);
        await _tree.RefreshAsync();
        _tree.EditSettingsCommand.Execute(_tree.Roots[0]);
        _tree.SettingsEditor!.Scripts.PreRequest.Source = "unsaved";

        _tree.CloseSettingsCommand.Execute(null);
        _tree.EditSettingsCommand.Execute(_tree.Roots[0]);

        Assert.Equal("saved", _tree.SettingsEditor!.Scripts.PreRequest.Source);
        Assert.False(_tree.SettingsEditor.IsDirty);
    }

    [Fact]
    public async Task ChainProviderReturnsTheSavedScriptsAfterTheSave()
    {
        var c = FolderDefinition.Create("c");
        await _store.SaveFolderAsync(c, null, CancellationToken.None);
        var request = HttpRequestDefinition.Create("r");
        await _requests.SaveAsync(request, CancellationToken.None);
        await _store.MoveAsync(request.Id, c.Id, CancellationToken.None);
        await _tree.RefreshAsync();
        var provider = new CollectionTreeScriptChainProvider(_tree);
        Assert.Equal("", provider.GetAncestors(request.Id).Steps.Single().Scripts.PreRequest.Source);
        _tree.EditSettingsCommand.Execute(_tree.Roots[0]);
        _tree.SettingsEditor!.Scripts.PreRequest.Source = "pm.test()";

        await _tree.SettingsEditor.SaveCommand.ExecuteAsync(null);
        await _tree.RefreshAsync();

        Assert.Equal("pm.test()", provider.GetAncestors(request.Id).Steps.Single().Scripts.PreRequest.Source);
    }
}
