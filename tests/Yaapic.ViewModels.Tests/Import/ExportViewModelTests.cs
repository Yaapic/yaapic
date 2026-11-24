using Yaapic.Core.Export;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Import;

namespace Yaapic.ViewModels.Tests.Import;

public sealed class ExportViewModelTests
{
    private sealed class FakeExporter(string id, bool requiresCollection, Func<ExportSource, ExportResult> export) : IExporter
    {
        public ExportSource? Received { get; private set; }

        public string Id => id;

        public string DisplayName => "Fake " + id;

        public string FileExtension => ".json";

        public bool RequiresCollection => requiresCollection;

        public ExportResult Export(ExportSource source)
        {
            Received = source;
            return export(source);
        }
    }

    private static ExportResult One(string path = "out.json", ImportReport? report = null) =>
        new([new ExportFile(path, [1, 2, 3])], report ?? new ImportReport());

    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryEnvironmentStore _environments = new();
    private readonly InMemoryGlobalsStore _globals = new();
    private readonly FakeDialogService _dialogs = new() { SaveFileAnswer = true };
    private readonly InMemoryCollectionStore _collections;
    private readonly FolderDefinition _alpha = FolderDefinition.Create("Alpha");
    private readonly FolderDefinition _zulu = FolderDefinition.Create("Zulu");

    public ExportViewModelTests()
    {
        Localizer.Use(UiLanguage.English);
        _collections = new InMemoryCollectionStore(_requests);
    }

    private async Task<ExportViewModel> MakeAsync(Guid? preselected = null, params IExporter[] exporters)
    {
        await _collections.SaveFolderAsync(_zulu, null, CancellationToken.None);
        await _collections.SaveFolderAsync(_alpha, null, CancellationToken.None);
        var registry = new ExporterRegistry(exporters);
        var vm = new ExportViewModel(registry, new ExportSourceReader(_collections, _environments, _globals), _collections, _environments, _dialogs);
        await vm.LoadAsync(preselected);
        return vm;
    }

    private static FakeExporter Collection(Func<ExportSource, ExportResult>? export = null) =>
        new("postman", true, export ?? (_ => One()));

    private static FakeExporter EnvironmentOnly(Func<ExportSource, ExportResult>? export = null) =>
        new("postman-environment", false, export ?? (_ => One("env.json")));

    [Fact]
    public async Task Load_lists_sorted_collections_and_environments_and_selects_the_first()
    {
        var env = EnvironmentDefinition.Create("Local") with { CollectionId = _zulu.Id };
        await _environments.SaveAsync(env, CancellationToken.None);

        var vm = await MakeAsync(null, Collection(), EnvironmentOnly());

        Assert.Equal(["Alpha", "Zulu"], vm.Collections.Select(c => c.Name));
        Assert.Equal("Alpha", vm.SelectedCollection?.Name);
        var choice = Assert.Single(vm.EnvironmentChoices);
        Assert.Equal(("Local", "Zulu", false), (choice.Name, choice.CollectionName, choice.IsSelected));
        Assert.Equal("postman", vm.SelectedFormat?.Id);
    }

    [Fact]
    public async Task Load_selects_the_preselected_collection()
    {
        var vm = await MakeAsync(_zulu.Id, Collection());

        Assert.Equal(_zulu.Id, vm.SelectedCollection?.Id);
    }

    [Fact]
    public async Task Exporting_one_file_saves_it_by_its_name()
    {
        var vm = await MakeAsync(null, Collection());

        await vm.ExportCommand.ExecuteAsync(null);

        var saved = Assert.Single(_dialogs.SavedFiles);
        Assert.Equal("out.json", saved.Name);
        Assert.Equal(new byte[] { 1, 2, 3 }, saved.Content);
        Assert.True(vm.Saved);
        Assert.Equal("out.json", vm.SavedName);
        Assert.False(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Several_files_are_saved_as_a_zip_named_after_the_collection()
    {
        var many = Collection(_ => new([new ExportFile("a.bru", [1]), new ExportFile("b.bru", [2])], new ImportReport()));
        var vm = await MakeAsync(null, many);

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("alpha.zip", Assert.Single(_dialogs.SavedFiles).Name);
    }

    [Fact]
    public async Task A_canceled_save_is_not_an_error()
    {
        _dialogs.SaveFileAnswer = false;
        var vm = await MakeAsync(null, Collection());

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.False(vm.Saved);
        Assert.Null(vm.SavedName);
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task The_environment_exporter_uses_the_selected_environments_and_globals()
    {
        var a = EnvironmentDefinition.Create("A");
        var b = EnvironmentDefinition.Create("B");
        await _environments.SaveAsync(a, CancellationToken.None);
        await _environments.SaveAsync(b, CancellationToken.None);
        await _globals.SaveAsync([new KeyValueEntry("g", "1")], CancellationToken.None);
        var exporter = EnvironmentOnly();
        var vm = await MakeAsync(null, exporter);
        Assert.False(vm.RequiresCollection);
        Assert.True(vm.ShowEnvironments);
        Assert.False(vm.ExportCommand.CanExecute(null));

        vm.EnvironmentChoices.Single(e => e.Name == "B").IsSelected = true;
        vm.IncludeGlobals = true;
        await vm.ExportCommand.ExecuteAsync(null);

        var source = exporter.Received!;
        Assert.Null(source.Collection);
        Assert.Equal(["B"], source.Environments.Select(e => e.Name));
        Assert.Equal(["g"], source.Globals.Select(g => g.Key));
    }

    [Fact]
    public async Task Globals_alone_enable_the_environment_export()
    {
        var vm = await MakeAsync(null, EnvironmentOnly());

        vm.IncludeGlobals = true;

        Assert.True(vm.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task The_collection_export_is_disabled_without_a_collection()
    {
        var vm = new ExportViewModel(new ExporterRegistry([Collection()]),
            new ExportSourceReader(_collections, _environments, _globals), _collections, _environments, _dialogs);
        await vm.LoadAsync(null);

        Assert.Null(vm.SelectedCollection);
        Assert.False(vm.ExportCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("postman", 0)]
    [InlineData("openapi-json", 0)]
    [InlineData("insomnia", 1)]
    [InlineData("bruno", 1)]
    public async Task Collection_formats_take_the_environments_scoped_to_the_collection(string id, int expected)
    {
        await _environments.SaveAsync(EnvironmentDefinition.Create("Mine") with { CollectionId = _alpha.Id }, CancellationToken.None);
        await _environments.SaveAsync(EnvironmentDefinition.Create("Other") with { CollectionId = _zulu.Id }, CancellationToken.None);
        await _environments.SaveAsync(EnvironmentDefinition.Create("Global"), CancellationToken.None);
        var exporter = new FakeExporter(id, true, _ => One());
        var vm = await MakeAsync(null, exporter);

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal(expected, exporter.Received!.Environments.Count);
        Assert.All(exporter.Received.Environments, e => Assert.Equal("Mine", e.Name));
    }

    [Fact]
    public async Task An_export_exception_sets_the_error()
    {
        var vm = await MakeAsync(null, Collection(_ => throw new ExportException("nope")));

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("nope", vm.Error);
        Assert.True(vm.HasError);
        Assert.Empty(_dialogs.SavedFiles);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task An_unexpected_exception_sets_a_wrapped_error()
    {
        var vm = await MakeAsync(null, Collection(_ => throw new InvalidOperationException("boom")));

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal("The export failed: boom", vm.Error);
    }

    [Fact]
    public async Task Secret_values_never_reach_the_exporter()
    {
        await _environments.SaveAsync(
            EnvironmentDefinition.Create("Env") with { Variables = [new KeyValueEntry("token", "s3cr3t", IsSecret: true)], CollectionId = _alpha.Id },
            CancellationToken.None);
        var exporter = new FakeExporter("insomnia", true, _ => One());
        var vm = await MakeAsync(null, exporter);

        await vm.ExportCommand.ExecuteAsync(null);

        var variable = Assert.Single(Assert.Single(exporter.Received!.Environments).Variables);
        Assert.Equal("", variable.Value);
    }

    [Fact]
    public async Task The_report_merges_the_reader_notes_and_the_exporter_entries()
    {
        await _environments.SaveAsync(
            EnvironmentDefinition.Create("Env") with { Variables = [new KeyValueEntry("token", "x", IsSecret: true)], CollectionId = _alpha.Id },
            CancellationToken.None);
        var exporter = new FakeExporter("insomnia", true, _ =>
        {
            var report = new ImportReport();
            report.Warning("r", "approximated");
            return One(report: report);
        });
        var vm = await MakeAsync(null, exporter);

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Equal([ImportSeverity.Info, ImportSeverity.Warning], vm.Report.Rows.Select(r => r.Severity));
        Assert.Equal(1, vm.Report.InfoCount);
        Assert.Equal(1, vm.Report.WarningCount);
    }

    [Fact]
    public async Task Leaving_the_environment_format_clears_the_globals_option()
    {
        var vm = await MakeAsync(null, Collection(), EnvironmentOnly());
        vm.SelectedFormat = vm.Formats.Single(f => f.Id == "postman-environment");
        vm.IncludeGlobals = true;

        vm.SelectedFormat = vm.Formats.Single(f => f.Id == "postman");

        Assert.False(vm.IncludeGlobals);
        Assert.False(vm.ShowEnvironments);
        Assert.True(vm.RequiresCollection);
    }
}
