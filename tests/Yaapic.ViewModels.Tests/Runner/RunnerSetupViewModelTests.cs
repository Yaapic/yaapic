using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Runner;

namespace Yaapic.ViewModels.Tests.Runner;

public sealed class RunnerSetupViewModelTests
{
    private sealed class FakeReader : IImportInputReader
    {
        public ImportInput? Input { get; set; }

        public ImportException? Failure { get; set; }

        public Task<ImportInput> ReadFileAsync(string path, CancellationToken ct) =>
            Failure is { } f ? Task.FromException<ImportInput>(f) : Task.FromResult(Input!);

        public Task<ImportInput> ReadFolderAsync(string path, CancellationToken ct) => throw new NotSupportedException();
    }

    private readonly FakeDialogService _dialogs = new();
    private readonly FakeReader _reader = new();
    private readonly RunnerSetupViewModel _vm;

    public RunnerSetupViewModelTests()
    {
        _vm = new RunnerSetupViewModel(_reader, _dialogs);
        _vm.Load(Folder("Root", Req("a"), Req("b"), Folder("Sub", Req("c"))));
    }

    private static CollectionNode Req(string name) => CollectionNode.ForRequest(HttpRequestDefinition.Create(name));

    private static CollectionNode Folder(string name, params CollectionNode[] children) =>
        CollectionNode.ForFolder(FolderDefinition.Create(name), children);

    private string[] Names => [.. _vm.Items.Select(i => i.Name)];

    private async Task ChooseAsync(string fileName, string text)
    {
        _dialogs.PickFileAnswer = fileName;
        _reader.Input = ImportInput.FromText(fileName, text);
        await _vm.ChooseDataFileCommand.ExecuteAsync(null);
    }

    [Fact]
    public void Load_lists_the_requests_with_their_paths()
    {
        Assert.Equal("Root", _vm.FolderName);
        Assert.Equal(["c", "a", "b"], Names);
        Assert.Equal(["Root/Sub", "Root", "Root"], _vm.Items.Select(i => i.Path));
        Assert.Equal("GET", _vm.Items[0].Method);
        Assert.All(_vm.Items, i => Assert.True(i.IsSelected));
        Assert.Equal(3, _vm.SelectedCount);
        Assert.True(_vm.CanRun);
    }

    [Fact]
    public void Move_up_and_down_reorder_and_are_disabled_at_the_ends()
    {
        RunnerItemViewModel first = _vm.Items[0];
        RunnerItemViewModel last = _vm.Items[2];
        Assert.False(_vm.MoveUpCommand.CanExecute(first));
        Assert.False(_vm.MoveDownCommand.CanExecute(last));

        _vm.MoveDownCommand.Execute(first);

        Assert.Equal(["a", "c", "b"], Names);
        Assert.True(_vm.MoveUpCommand.CanExecute(first));
        _vm.MoveUpCommand.Execute(first);
        Assert.Equal(["c", "a", "b"], Names);
    }

    [Fact]
    public void Select_all_and_none_update_the_count()
    {
        _vm.SelectNoneCommand.Execute(null);
        Assert.Equal(0, _vm.SelectedCount);
        _vm.Items[1].IsSelected = true;
        Assert.Equal(1, _vm.SelectedCount);
        _vm.SelectAllCommand.Execute(null);
        Assert.Equal(3, _vm.SelectedCount);
    }

    [Fact]
    public void Validation_reports_each_error()
    {
        _vm.Iterations = 0;
        Assert.Equal(Strings.RunnerIterationsRange, _vm.ValidationError);
        _vm.Iterations = 10001;
        Assert.Equal(Strings.RunnerIterationsRange, _vm.ValidationError);
        _vm.Iterations = 1;
        _vm.DelayMs = 60001;
        Assert.Equal(Strings.RunnerDelayRange, _vm.ValidationError);
        _vm.DelayMs = -1;
        Assert.Equal(Strings.RunnerDelayRange, _vm.ValidationError);
        _vm.DelayMs = 0;
        _vm.SelectNoneCommand.Execute(null);
        Assert.Equal(Strings.RunnerNothingSelected, _vm.ValidationError);
        Assert.False(_vm.CanRun);
        _vm.SelectAllCommand.Execute(null);
        Assert.Null(_vm.ValidationError);
        Assert.True(_vm.CanRun);
    }

    [Fact]
    public async Task Choosing_a_data_file_loads_columns_preview_and_iterations()
    {
        await ChooseAsync(Path.Combine(Path.GetTempPath(), "d", "users.csv"), "name,age\na,1\nb,2\nc,3\nd,4\ne,5\nf,6\n");

        Assert.True(_vm.HasData);
        Assert.Equal("users.csv", _vm.DataFileName);
        Assert.Equal(6, _vm.DataRowCount);
        Assert.Equal(["name", "age"], _vm.DataColumns);
        Assert.Equal(5, _vm.DataPreview.Count);
        Assert.Equal(["a", "1"], _vm.DataPreview[0]);
        Assert.Equal(6, _vm.Iterations);
        Assert.Null(_vm.DataError);
    }

    [Fact]
    public async Task Missing_cells_of_the_preview_are_empty()
    {
        await ChooseAsync("d.json", """[{"a":"1"},{"b":"2"}]""");

        Assert.Equal(["a", "b"], _vm.DataColumns);
        Assert.Equal(["1", ""], _vm.DataPreview[0]);
        Assert.Equal(["", "2"], _vm.DataPreview[1]);
    }

    [Fact]
    public async Task A_canceled_picker_changes_nothing()
    {
        _dialogs.PickFileAnswer = null;

        await _vm.ChooseDataFileCommand.ExecuteAsync(null);

        Assert.False(_vm.HasData);
        Assert.Null(_vm.DataError);
        Assert.Equal(1, _vm.Iterations);
    }

    [Fact]
    public async Task A_reader_error_sets_the_data_error_and_clears_the_data()
    {
        await ChooseAsync("a.csv", "x\n1\n");
        _reader.Failure = new ImportException("boom");

        await _vm.ChooseDataFileCommand.ExecuteAsync(null);

        Assert.Equal("boom", _vm.DataError);
        Assert.False(_vm.HasData);
        Assert.Empty(_vm.DataColumns);
    }

    [Fact]
    public async Task A_parse_error_sets_the_data_error()
    {
        await ChooseAsync("a.json", "{}");

        Assert.Equal(Strings.RunnerDataJsonNotArray, _vm.DataError);
        Assert.False(_vm.HasData);
        Assert.False(_vm.IsLoadingData);
    }

    [Fact]
    public async Task Clear_data_removes_the_file()
    {
        await ChooseAsync("a.csv", "x\n1\n2\n");

        _vm.ClearDataCommand.Execute(null);

        Assert.False(_vm.HasData);
        Assert.Equal(0, _vm.DataRowCount);
        Assert.Empty(_vm.DataPreview);
        Assert.Empty(_vm.BuildPlan().Data);
    }

    [Fact]
    public async Task Build_plan_keeps_order_selection_options_and_rows()
    {
        await ChooseAsync("a.csv", "x\n1\n2\n");
        _vm.MoveDownCommand.Execute(_vm.Items[0]);
        _vm.Items[0].IsSelected = false;
        _vm.DelayMs = 25;
        _vm.StopOnFailure = true;
        _vm.PersistVariables = false;

        var plan = _vm.BuildPlan();

        Assert.Equal("Root", plan.Name);
        Assert.Equal(["c", "b"], plan.Items.Select(i => i.Request.Name));
        Assert.Equal(2, plan.Options.Iterations);
        Assert.Equal(25, plan.Options.DelayMs);
        Assert.True(plan.Options.StopOnFailure);
        Assert.False(plan.Options.PersistVariables);
        Assert.Equal(2, plan.Data.Count);
    }

    [Fact]
    public void Build_plan_throws_when_the_run_cannot_start()
    {
        _vm.SelectNoneCommand.Execute(null);

        Assert.Throws<InvalidOperationException>(() => _vm.BuildPlan());
    }

    [Fact]
    public async Task Load_resets_options_and_data()
    {
        await ChooseAsync("a.csv", "x\n1\n2\n");
        _vm.StopOnFailure = true;

        _vm.Load(Folder("Other", Req("z")));

        Assert.Equal(1, _vm.Iterations);
        Assert.False(_vm.StopOnFailure);
        Assert.False(_vm.HasData);
        Assert.Equal(["z"], Names);
    }
}
