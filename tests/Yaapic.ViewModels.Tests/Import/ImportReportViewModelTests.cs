using Yaapic.Core.Import;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Import;

namespace Yaapic.ViewModels.Tests.Import;

public sealed class ImportReportViewModelTests
{
    private static ImportReport Sample()
    {
        var report = new ImportReport();
        report.Info("a", "note");
        report.Warning("b/c", "warn");
        report.Skipped("", "skip");
        report.Warning("d", "warn2");
        return report;
    }

    private static ImportReportViewModel Make(FakeDialogService? dialogs = null)
    {
        Localizer.Use(UiLanguage.English);
        return new(dialogs ?? new FakeDialogService());
    }

    [Fact]
    public void Load_counts_labels_and_summarizes()
    {
        var vm = Make();
        vm.Load(Sample());

        Assert.Equal((1, 2, 1), (vm.InfoCount, vm.WarningCount, vm.SkippedCount));
        Assert.True(vm.HasRows);
        Assert.True(vm.HasProblems);
        Assert.Equal("2 warnings, 1 skipped, 1 notes", vm.Summary);
        Assert.Equal(["Note", "Warning", "Skipped", "Warning"], vm.Rows.Select(r => r.SeverityLabel));
    }

    [Fact]
    public void Empty_and_null_reports_clear_the_rows()
    {
        var vm = Make();
        vm.Load(Sample());
        vm.Load(null);

        Assert.Empty(vm.Rows);
        Assert.False(vm.HasRows);
        Assert.False(vm.HasProblems);
        Assert.Equal("Nothing to report.", vm.Summary);
        Assert.False(vm.CopyCommand.CanExecute(null));

        vm.Load(new ImportReport());
        Assert.Equal("Nothing to report.", vm.Summary);
    }

    [Fact]
    public void Info_only_has_no_problems()
    {
        var vm = Make();
        var report = new ImportReport();
        report.Info("a", "n");
        vm.Load(report);

        Assert.False(vm.HasProblems);
    }

    [Theory]
    [InlineData(ImportSeverity.Info)]
    [InlineData(ImportSeverity.Warning)]
    [InlineData(ImportSeverity.Skipped)]
    public void Each_filter_hides_its_severity_and_raises_VisibleRows(ImportSeverity severity)
    {
        var vm = Make();
        vm.Load(Sample());
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        switch (severity)
        {
            case ImportSeverity.Info: vm.ShowInfo = false; break;
            case ImportSeverity.Warning: vm.ShowWarnings = false; break;
            default: vm.ShowSkipped = false; break;
        }

        Assert.Contains(nameof(ImportReportViewModel.VisibleRows), raised);
        Assert.DoesNotContain(vm.VisibleRows, r => r.Severity == severity);
        Assert.Equal(4, vm.Rows.Count);
    }

    [Fact]
    public void AsText_ignores_filters_and_handles_empty_paths()
    {
        var vm = Make();
        vm.Load(Sample());
        vm.ShowWarnings = false;

        Assert.Equal(
            "2 warnings, 1 skipped, 1 notes\n\n[Note] a: note\n[Warning] b/c: warn\n[Skipped] skip\n[Warning] d: warn2",
            vm.AsText());
    }

    [Fact]
    public async Task Copy_puts_the_text_on_the_clipboard()
    {
        var dialogs = new FakeDialogService();
        var vm = Make(dialogs);
        vm.Load(Sample());

        Assert.True(vm.CopyCommand.CanExecute(null));
        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal([vm.AsText()], dialogs.ClipboardTexts);
    }
}
