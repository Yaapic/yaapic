using Yaapic.Core.Http;
using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class TabsViewModelTests
{
    private sealed class NoEnvironment : IActiveEnvironmentProvider
    {
        public EnvironmentDefinition? Current => null;
    }

    private sealed class FailingStore : IRequestStore
    {
        public Task<IReadOnlyList<HttpRequestDefinition>> ListAsync(CancellationToken ct) => throw new NotSupportedException();

        public Task SaveAsync(HttpRequestDefinition request, CancellationToken ct) => throw new StorageException("disk full");

        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private readonly InMemoryRequestStore _store = new();
    private readonly FakeDialogService _dialogs = new();

    private TabsViewModel Create(IRequestStore? store = null)
    {
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        return new TabsViewModel(
            store ?? _store,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new NoEnvironment(), isNew));
    }

    private static HttpRequestDefinition Saved(string name) => HttpRequestDefinition.Create(name);

    [Fact]
    public void NewTabIsAppendedAndActivated()
    {
        var tabs = Create();

        tabs.NewTab();

        var tab = Assert.Single(tabs.Tabs);
        Assert.Same(tab, tabs.ActiveTab);
        Assert.True(tab.IsNew);
    }

    [Fact]
    public void OpeningAnOpenIdActivatesWithoutDuplicating()
    {
        var tabs = Create();
        var a = Saved("a");
        tabs.Open(a);
        tabs.Open(Saved("b"));

        tabs.Open(a);

        Assert.Equal(2, tabs.Tabs.Count);
        Assert.Equal(a.Id, tabs.ActiveTab!.Id);
    }

    [Fact]
    public async Task SaveWritesClearsDirtyAndRaisesEvent()
    {
        var tabs = Create();
        tabs.NewTab();
        var tab = tabs.ActiveTab!;
        tab.Name = "Users";
        HttpRequestDefinition? raised = null;
        tabs.RequestSaved += (_, d) => raised = d;

        Assert.True(await tabs.SaveAsync(tab));

        Assert.False(tab.IsDirty);
        Assert.Equal("Users", Assert.Single(await _store.ListAsync(TestContext.Current.CancellationToken)).Name);
        Assert.Equal(tab.Id, raised!.Id);
    }

    [Fact]
    public async Task SaveFailureSetsLastError()
    {
        var tabs = Create(new FailingStore());
        tabs.NewTab();

        Assert.False(await tabs.SaveAsync(tabs.ActiveTab!));

        Assert.Equal("disk full", tabs.LastError);
        Assert.True(tabs.ActiveTab!.IsDirty);
    }

    [Fact]
    public async Task SaveActiveCommandSavesActiveTab()
    {
        var tabs = Create();
        tabs.NewTab();

        await tabs.SaveActiveCommand.ExecuteAsync(null);

        Assert.Equal(1, _store.SaveCount);
    }

    [Fact]
    public async Task CleanTabClosesWithoutPrompt()
    {
        var tabs = Create();
        tabs.Open(Saved("a"));

        Assert.True(await tabs.CloseAsync(tabs.ActiveTab!));

        Assert.Empty(tabs.Tabs);
        Assert.Empty(_dialogs.UnsavedPrompts);
    }

    [Fact]
    public async Task UntouchedNewTabClosesWithoutPrompt()
    {
        var tabs = Create();
        tabs.NewTab();

        Assert.True(await tabs.CloseAsync(tabs.ActiveTab!));

        Assert.Empty(_dialogs.UnsavedPrompts);
        Assert.Null(tabs.ActiveTab);
    }

    [Fact]
    public async Task EditedNewTabPrompts()
    {
        var tabs = Create();
        tabs.NewTab();
        tabs.ActiveTab!.Url = "https://x.test";

        await tabs.CloseAsync(tabs.ActiveTab);

        Assert.Equal(["New Request"], _dialogs.UnsavedPrompts);
    }

    [Fact]
    public async Task DirtyCloseWithSaveSavesThenCloses()
    {
        var tabs = Create();
        tabs.Open(Saved("a"));
        tabs.ActiveTab!.Url = "https://x.test";
        _dialogs.UnsavedAnswer = UnsavedChoice.Save;

        Assert.True(await tabs.CloseAsync(tabs.ActiveTab));

        Assert.Equal(1, _store.SaveCount);
        Assert.Empty(tabs.Tabs);
    }

    [Fact]
    public async Task DirtyCloseWithSaveFailureKeepsTabOpen()
    {
        var tabs = Create(new FailingStore());
        tabs.Open(Saved("a"));
        tabs.ActiveTab!.Url = "https://x.test";
        _dialogs.UnsavedAnswer = UnsavedChoice.Save;

        Assert.False(await tabs.CloseAsync(tabs.ActiveTab));

        Assert.Single(tabs.Tabs);
    }

    [Fact]
    public async Task DirtyCloseWithDiscardClosesWithoutSaving()
    {
        var tabs = Create();
        tabs.Open(Saved("a"));
        tabs.ActiveTab!.Url = "https://x.test";
        _dialogs.UnsavedAnswer = UnsavedChoice.Discard;

        Assert.True(await tabs.CloseAsync(tabs.ActiveTab));

        Assert.Equal(0, _store.SaveCount);
        Assert.Empty(tabs.Tabs);
    }

    [Fact]
    public async Task DirtyCloseWithCancelKeepsTab()
    {
        var tabs = Create();
        tabs.Open(Saved("a"));
        tabs.ActiveTab!.Url = "https://x.test";

        Assert.False(await tabs.CloseAsync(tabs.ActiveTab));

        Assert.Single(tabs.Tabs);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(0, 0)]
    public async Task ClosingActiveTabActivatesNeighbour(int closeIndex, int expectedActiveIndex)
    {
        var tabs = Create();
        foreach (var name in new[] { "a", "b", "c" })
        {
            tabs.Open(Saved(name));
        }

        tabs.ActiveTab = tabs.Tabs[closeIndex];
        await tabs.CloseAsync(tabs.ActiveTab);

        Assert.Equal(2, tabs.Tabs.Count);
        Assert.Same(tabs.Tabs[expectedActiveIndex], tabs.ActiveTab);
    }

    [Fact]
    public async Task ClosingOnlyTabLeavesNoActiveTab()
    {
        var tabs = Create();
        tabs.Open(Saved("a"));

        await tabs.CloseActiveCommand.ExecuteAsync(null);

        Assert.Null(tabs.ActiveTab);
    }

    [Fact]
    public void CloseWithoutPromptRemovesTabById()
    {
        var tabs = Create();
        var a = Saved("a");
        tabs.Open(a);
        tabs.ActiveTab!.Url = "https://x.test";

        tabs.CloseWithoutPrompt(a.Id);

        Assert.Empty(tabs.Tabs);
        Assert.Empty(_dialogs.UnsavedPrompts);
    }

    [Fact]
    public async Task SessionChangedIsRaisedOnAddRemoveAndActivation()
    {
        var tabs = Create();
        var count = 0;
        tabs.SessionChanged += (_, _) => count++;

        tabs.Open(Saved("a"));
        tabs.Open(Saved("b"));
        var afterOpen = count;
        tabs.ActiveTab = tabs.Tabs[0];
        var afterSwitch = count;
        await tabs.CloseAsync(tabs.Tabs[1]);

        Assert.True(afterOpen >= 4);
        Assert.True(afterSwitch > afterOpen);
        Assert.True(count > afterSwitch);
    }
}
