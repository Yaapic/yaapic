using Yaapic.Core.Http;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.History;
using Yaapic.ViewModels.Import;
using Yaapic.ViewModels.Palette;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.ViewModels.Tests;

public sealed class ImportExportShellTests
{
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryCollectionStore _collections;
    private readonly FakeDialogService _dialogs = new();
    private readonly ShellParts _parts = new();
    private readonly ShellViewModel _shell;

    public ImportExportShellTests()
    {
        _collections = new InMemoryCollectionStore(_requests);
        var sender = new RequestSender(new FakeHttpTransport((_, _) => throw new NotSupportedException()));
        var tabs = new TabsViewModel(
            _requests,
            _dialogs,
            (definition, isNew) => RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider(), isNew));
        _shell = new ShellViewModel(
            tabs,
            new RequestListViewModel(_requests, tabs, _dialogs),
            new CollectionTreeViewModel(_collections, _requests, tabs, _dialogs),
            new HistoryViewModel(new InMemoryHistoryStore(), TimeProvider.System, tabs, _dialogs),
            new EnvironmentsViewModel(new InMemoryEnvironmentStore(), _dialogs),
            new GlobalsViewModel(new InMemoryGlobalsStore()),
            new InMemorySessionStore(),
            _dialogs,
            _parts.Console, _parts.VaultViewModel, _parts.Engines, _parts.Updates);
    }

    private void Attach() => _shell.AttachImportExport(() => null!, () => null!);

    private string[] Slugs() => [.. new CommandsPaletteSource(_shell).GetItems().Select(i => i.Id)];

    [Fact]
    public void Requests_raise_their_events_when_attached()
    {
        Attach();
        var imports = 0;
        var exports = new List<Guid?>();
        _shell.ImportRequested += (_, _) => imports++;
        _shell.ExportRequested += (_, id) => exports.Add(id);
        var id = Guid.NewGuid();

        _shell.RequestImport();
        _shell.RequestExport(id);
        _shell.RequestExport();

        Assert.Equal(1, imports);
        Assert.Equal([id, (Guid?)null], exports);
    }

    [Fact]
    public void Requests_do_nothing_when_not_attached()
    {
        var raised = 0;
        _shell.ImportRequested += (_, _) => raised++;
        _shell.ExportRequested += (_, _) => raised++;

        _shell.RequestImport();
        _shell.RequestExport(Guid.NewGuid());

        Assert.Equal(0, raised);
        Assert.DoesNotContain("command:import", Slugs());
        Assert.DoesNotContain("command:export-collection", Slugs());
        Assert.Throws<InvalidOperationException>(_shell.CreateImport);
        Assert.Throws<InvalidOperationException>(_shell.CreateExport);
    }

    [Fact]
    public void Attaching_twice_throws()
    {
        Attach();

        Assert.Throws<InvalidOperationException>(Attach);
    }

    [Fact]
    public void Create_calls_the_factories()
    {
        int imports = 0, exports = 0;
        _shell.AttachImportExport(() => { imports++; return null!; }, () => { exports++; return null!; });

        _shell.CreateImport();
        _shell.CreateExport();
        _shell.CreateExport();

        Assert.Equal((1, 2), (imports, exports));
    }

    [Fact]
    public void The_palette_offers_import_and_export_and_runs_them()
    {
        Attach();
        var raised = new List<string>();
        _shell.ImportRequested += (_, _) => raised.Add("import");
        _shell.ExportRequested += (_, id) => raised.Add($"export:{id}");

        var items = new CommandsPaletteSource(_shell).GetItems();
        items.Single(i => i.Id == "command:import").Execute();
        items.Single(i => i.Id == "command:export-collection").Execute();

        Assert.Equal(["import", "export:"], raised);
        Assert.DoesNotContain("command:export-this-collection", items.Select(i => i.Id));
    }

    [Fact]
    public async Task The_palette_offers_exporting_the_collection_of_the_active_tab_only_for_filed_requests()
    {
        Attach();
        var folder = FolderDefinition.Create("Billing");
        await _collections.SaveFolderAsync(folder, null, CancellationToken.None);
        var filed = HttpRequestDefinition.Create("Filed");
        await _requests.SaveAsync(filed, CancellationToken.None);
        await _collections.MoveAsync(filed.Id, folder.Id, CancellationToken.None);
        var loose = HttpRequestDefinition.Create("Loose");
        await _requests.SaveAsync(loose, CancellationToken.None);
        await _shell.Collections.RefreshAsync();
        Guid? exported = null;
        _shell.ExportRequested += (_, id) => exported = id;

        _shell.Tabs.Open(loose);
        Assert.DoesNotContain("command:export-this-collection", Slugs());

        _shell.Tabs.Open(filed);
        var item = new CommandsPaletteSource(_shell).GetItems().Single(i => i.Id == "command:export-this-collection");
        Assert.Equal("Export collection \"Billing\"...", item.Title);
        await item.Execute();
        Assert.Equal(folder.Id, exported);
    }

    [Fact]
    public async Task ExportCollectionCommand_is_enabled_for_a_top_level_folder_only()
    {
        Attach();
        var top = FolderDefinition.Create("Top");
        var nested = FolderDefinition.Create("Nested");
        await _collections.SaveFolderAsync(top, null, CancellationToken.None);
        await _collections.SaveFolderAsync(nested, top.Id, CancellationToken.None);
        var request = HttpRequestDefinition.Create("R");
        await _requests.SaveAsync(request, CancellationToken.None);
        await _collections.MoveAsync(request.Id, top.Id, CancellationToken.None);
        await _shell.Collections.RefreshAsync();
        var root = _shell.Collections.Roots.Single();
        root.IsExpanded = true;
        Guid? exported = null;
        _shell.ExportRequested += (_, id) => exported = id;

        Assert.True(_shell.ExportCollectionCommand.CanExecute(root));
        Assert.False(_shell.ExportCollectionCommand.CanExecute(root.Children.Single(c => c.IsFolder)));
        Assert.False(_shell.ExportCollectionCommand.CanExecute(root.Children.Single(c => !c.IsFolder)));

        _shell.ExportCollectionCommand.Execute(root);
        Assert.Equal(top.Id, exported);
    }
}
