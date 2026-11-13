using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Environments;

namespace Yaapic.ViewModels.Tests;

public sealed class EnvironmentScopeTests
{
    private readonly InMemoryEnvironmentStore _store = new();
    private readonly EnvironmentsViewModel _vm;
    private readonly FolderDefinition _shop = FolderDefinition.Create("Shop");
    private readonly FolderDefinition _blog = FolderDefinition.Create("Blog");

    public EnvironmentScopeTests() => _vm = new EnvironmentsViewModel(_store, new FakeDialogService());

    private async Task<EnvironmentItemViewModel> SeedAsync(string name, Guid? collection)
    {
        await _store.SaveAsync(new EnvironmentDefinition(Guid.NewGuid(), name, [], collection), CancellationToken.None);
        await _vm.LoadAsync(null);
        _vm.SetCollections([_shop, _blog]);
        return _vm.Environments.First(e => e.Name == name);
    }

    private static string[] Names(IEnumerable<EnvironmentItemViewModel> items) => [.. items.Select(i => i.Name)];

    [Fact]
    public async Task A_collection_sees_its_own_and_untied_environments()
    {
        await SeedAsync("any", null);
        await SeedAsync("shop", _shop.Id);
        await SeedAsync("blog", _blog.Id);

        _vm.SetScope(_shop.Id);

        Assert.Equal(["any", "shop"], Names(_vm.GetAvailable()));
    }

    [Fact]
    public async Task A_request_outside_any_collection_sees_only_untied_environments()
    {
        await SeedAsync("any", null);
        await SeedAsync("shop", _shop.Id);

        _vm.SetScope(null);

        Assert.Equal(["any"], Names(_vm.GetAvailable()));
    }

    [Fact]
    public async Task An_environment_tied_to_a_deleted_collection_stays_available()
    {
        await SeedAsync("orphan", Guid.NewGuid());

        _vm.SetScope(_shop.Id);

        Assert.Equal(["orphan"], Names(_vm.GetAvailable()));
        Assert.Null(_vm.Environments[0].SelectedCollection!.Id);
    }

    [Fact]
    public async Task Leaving_the_collection_deselects_its_environment_and_coming_back_restores_it()
    {
        var shop = await SeedAsync("shop", _shop.Id);
        _vm.SetScope(_shop.Id);
        _vm.ActiveEnvironment = shop;

        _vm.SetScope(_blog.Id);
        Assert.Null(_vm.ActiveEnvironment);

        _vm.SetScope(_shop.Id);
        Assert.Same(shop, _vm.ActiveEnvironment);
    }

    [Fact]
    public async Task An_untied_active_environment_follows_the_user_across_collections()
    {
        var any = await SeedAsync("any", null);
        _vm.ActiveEnvironment = any;

        _vm.SetScope(_shop.Id);

        Assert.Same(any, _vm.ActiveEnvironment);
    }

    [Fact]
    public async Task Tying_the_active_environment_to_another_collection_deselects_it()
    {
        var any = await SeedAsync("any", null);
        _vm.SetScope(_shop.Id);
        _vm.ActiveEnvironment = any;

        any.SelectedCollection = _vm.CollectionChoices.First(c => c.Id == _blog.Id);

        Assert.Null(_vm.ActiveEnvironment);
        Assert.True(any.IsDirty);
        Assert.Equal(_blog.Id, any.ToDefinition().CollectionId);
    }
}
