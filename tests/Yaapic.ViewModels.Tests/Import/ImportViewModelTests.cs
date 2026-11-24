using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Import;

namespace Yaapic.ViewModels.Tests.Import;

public sealed class ImportViewModelTests
{
    private sealed class FakeImporter(string id, string name, Func<ImportInput, ImportContext, ImportBundle> import) : IImporter
    {
        public string Id => id;

        public string DisplayName => name;

        public ImportConfidence Sniff(ImportInput input) =>
            input.Single.Text.StartsWith(id, StringComparison.Ordinal) ? ImportConfidence.Certain : ImportConfidence.None;

        public ImportBundle Import(ImportInput input, ImportContext context) => import(input, context);
    }

    private sealed class FakeReader : IImportInputReader
    {
        public ImportInput? Input { get; set; }

        public ImportException? Failure { get; set; }

        public Task<ImportInput> ReadFileAsync(string path, CancellationToken ct) => Read();

        public Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct) => Read();

        private Task<ImportInput> Read() => Failure is { } f ? Task.FromException<ImportInput>(f) : Task.FromResult(Input!);
    }

    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryEnvironmentStore _environments = new();
    private readonly InMemoryGlobalsStore _globals = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeReader _reader = new() { Input = ImportInput.FromText("api.json", "pm") };
    private int _reloads;

    private static ImportBundle Bundle(ImportContext context, int requests, int environments = 0, ImportReport? report = null)
    {
        foreach (var e in (report ?? new ImportReport()).Entries)
        {
            context.Report.Add(e.Severity, e.Path, e.Message);
        }

        var children = Enumerable.Range(0, requests)
            .Select(i => (ImportItem)new ImportRequest(HttpRequestDefinition.Create($"r{i}")))
            .ToList();
        var root = requests > 0 ? new ImportFolder(FolderDefinition.Create("API"), children) : null;
        var envs = Enumerable.Range(0, environments).Select(i => EnvironmentDefinition.Create($"e{i}")).ToList();
        return new ImportBundle(root, [], envs, [], context.Report);
    }

    private ImportViewModel Make(params IImporter[] importers)
    {
        Localizer.Use(UiLanguage.English);
        IImporter[] all = importers.Length > 0
            ? importers
            : [new FakeImporter("pm", "Postman", (_, c) => Bundle(c, 2, 1)), new FakeImporter("har", "HAR", (_, c) => Bundle(c, 1))];
        var registry = new ImporterRegistry(all);
        var collections = new InMemoryCollectionStore(_requests);
        var applier = new ImportApplier(_requests, collections, _environments, _globals, () => true);
        return new ImportViewModel(new ImportService(registry, applier), registry, _reader, _dialogs, () =>
        {
            _reloads++;
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void Auto_comes_first_then_one_format_per_importer()
    {
        var vm = Make();

        Assert.Equal([null, "pm", "har"], vm.Formats.Select(f => f.Id));
        Assert.Equal("Detect automatically", vm.Formats[0].Name);
        Assert.Same(vm.Formats[0], vm.SelectedFormat);
    }

    [Fact]
    public async Task Choosing_a_file_previews_counts_report_and_detected_format()
    {
        var vm = Make();
        _dialogs.PickFileAnswer = Path.Combine("data", "api.json");

        await vm.ChooseFileCommand.ExecuteAsync(null);

        Assert.Equal("api.json", vm.SourceName);
        Assert.Equal("Postman", vm.DetectedFormat);
        Assert.Equal((1, 2, 1, 0), (vm.Folders, vm.Requests, vm.Environments, vm.GlobalVariables));
        Assert.True(vm.HasPreview);
        Assert.False(vm.IsBusy);
        Assert.False(vm.HasError);
        Assert.True(vm.ImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_canceled_picker_changes_nothing()
    {
        var vm = Make();
        _dialogs.PickFileAnswer = null;
        _dialogs.PickFolderAnswer = null;

        await vm.ChooseFileCommand.ExecuteAsync(null);
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        Assert.False(vm.HasPreview);
        Assert.False(vm.HasError);
        Assert.Null(vm.SourceName);
        Assert.Equal(["Choose a Bruno collection folder"], _dialogs.PickFolderTitles);
    }

    [Fact]
    public async Task A_reader_exception_sets_the_error_and_clears_the_preview()
    {
        var vm = Make();
        _dialogs.PickFileAnswer = "a.json";
        await vm.ChooseFileCommand.ExecuteAsync(null);
        Assert.True(vm.HasPreview);

        _reader.Failure = new ImportException("File not found: a.json");
        await vm.ChooseFileCommand.ExecuteAsync(null);

        Assert.Equal("File not found: a.json", vm.Error);
        Assert.True(vm.HasError);
        Assert.False(vm.HasPreview);
        Assert.False(vm.IsBusy);
        Assert.Equal(0, vm.Requests);
    }

    [Fact]
    public async Task Choosing_a_folder_reads_the_folder()
    {
        var vm = Make();
        _dialogs.PickFolderAnswer = Path.Combine("data", "bruno") + Path.DirectorySeparatorChar;

        await vm.ChooseFolderCommand.ExecuteAsync(null);

        Assert.Equal("bruno", vm.SourceName);
        Assert.True(vm.HasPreview);
    }

    [Fact]
    public async Task Pasted_text_previews_and_blank_text_disables_the_command()
    {
        var vm = Make();
        Assert.False(vm.PreviewPastedCommand.CanExecute(null));

        vm.PastedText = "pm {}";
        Assert.True(vm.PreviewPastedCommand.CanExecute(null));
        await vm.PreviewPastedCommand.ExecuteAsync(null);

        Assert.Equal("Pasted text", vm.SourceName);
        Assert.Equal("Postman", vm.DetectedFormat);
        Assert.True(vm.HasPreview);
    }

    [Fact]
    public async Task Unrecognized_text_sets_the_error_then_a_manual_format_previews()
    {
        var vm = Make();
        vm.PastedText = "unknown";

        await vm.PreviewPastedCommand.ExecuteAsync(null);

        Assert.False(vm.HasPreview);
        Assert.Equal(Strings.ImportFormatNotRecognized, vm.Error);

        vm.SelectedFormat = vm.Formats.Single(f => f.Id == "pm");
        await vm.Completion;

        Assert.True(vm.HasPreview);
        Assert.Null(vm.Error);
        Assert.Equal("Postman", vm.DetectedFormat);
    }

    [Fact]
    public async Task The_static_resources_option_re_previews_and_shows_only_for_har()
    {
        var seen = new List<bool>();
        var har = new FakeImporter("har", "HAR", (_, c) =>
        {
            seen.Add(c.Options.IncludeStaticResources);
            return Bundle(c, 1);
        });
        var vm = Make(har, new FakeImporter("pm", "Postman", (_, c) => Bundle(c, 1)));
        vm.PastedText = "har";
        await vm.PreviewPastedCommand.ExecuteAsync(null);

        Assert.True(vm.ShowStaticResourcesOption);

        vm.IncludeStaticResources = true;
        await vm.Completion;

        Assert.Equal([false, true], seen);

        vm.PastedText = "pm";
        await vm.PreviewPastedCommand.ExecuteAsync(null);

        Assert.False(vm.ShowStaticResourcesOption);
    }

    [Fact]
    public async Task Importing_applies_reloads_replaces_the_report_and_raises_Imported()
    {
        var info = new ImportReport();
        info.Warning("x", "preview warning");
        var vm = Make(new FakeImporter("pm", "Postman", (_, c) => Bundle(c, 2, 1, info)));
        vm.PastedText = "pm";
        await vm.PreviewPastedCommand.ExecuteAsync(null);
        Assert.Contains(vm.Report.Rows, r => r.Message == "preview warning");
        ImportOutcome? raised = null;
        vm.Imported += (_, o) => raised = o;

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Equal(1, _reloads);
        Assert.True(vm.IsDone);
        Assert.False(vm.IsBusy);
        Assert.NotNull(vm.Outcome);
        Assert.Same(vm.Outcome, raised);
        Assert.Equal("Imported 2 requests in 1 folders and 1 environments.", vm.ResultSummary);
        Assert.Equal(2, (await _requests.ListAsync(TestContext.Current.CancellationToken)).Count);
        Assert.Equal(vm.Outcome.Report.Entries.Count, vm.Report.Rows.Count);
        Assert.False(vm.ImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Importing_is_disabled_for_an_empty_bundle()
    {
        var vm = Make(new FakeImporter("pm", "Postman", (_, c) => Bundle(c, 0)));
        vm.PastedText = "pm";

        await vm.PreviewPastedCommand.ExecuteAsync(null);

        Assert.True(vm.HasPreview);
        Assert.False(vm.ImportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Reset_clears_everything()
    {
        var vm = Make();
        vm.PastedText = "pm";
        await vm.PreviewPastedCommand.ExecuteAsync(null);
        await vm.ImportCommand.ExecuteAsync(null);
        vm.SelectedFormat = vm.Formats[1];
        await vm.Completion;

        vm.Reset();

        Assert.False(vm.HasPreview);
        Assert.False(vm.IsDone);
        Assert.Null(vm.Outcome);
        Assert.Null(vm.ResultSummary);
        Assert.Null(vm.Error);
        Assert.Null(vm.SourceName);
        Assert.Equal("", vm.PastedText);
        Assert.Same(vm.Formats[0], vm.SelectedFormat);
        Assert.False(vm.Report.HasRows);
        Assert.Equal((0, 0, 0), (vm.Folders, vm.Requests, vm.Environments));
    }

    [Fact]
    public async Task A_newer_preview_cancels_the_older_one()
    {
        using var gate = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        var slow = new FakeImporter("pm", "Postman", (input, c) =>
        {
            if (input.Single.Text.EndsWith("slow", StringComparison.Ordinal))
            {
                entered.Set();
                gate.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
                return Bundle(c, 5);
            }

            return Bundle(c, 1);
        });
        var vm = Make(slow);

        vm.PastedText = "pm slow";
        var first = vm.PreviewPastedCommand.ExecuteAsync(null);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        vm.PastedText = "pm fast";
        await vm.PreviewPastedCommand.ExecuteAsync(null);
        gate.Set();
        await first;

        Assert.Equal(1, vm.Requests);
        Assert.True(vm.HasPreview);
        Assert.False(vm.IsBusy);
    }
}
