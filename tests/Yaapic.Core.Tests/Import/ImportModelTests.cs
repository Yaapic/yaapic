using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import;

public class ImportModelTests
{
    [Fact]
    public void Report_counts_by_severity_and_flags_problems()
    {
        var report = new ImportReport();
        report.Info("a", "i");
        Assert.False(report.HasProblems);
        report.Warning("b", "w");
        report.Skipped("c", "s");
        report.Add(ImportSeverity.Warning, "d", "w2");
        Assert.True(report.HasProblems);
        Assert.Equal(1, report.Count(ImportSeverity.Info));
        Assert.Equal(2, report.Count(ImportSeverity.Warning));
        Assert.Equal(1, report.Count(ImportSeverity.Skipped));
        Assert.Equal(4, report.Entries.Count);
    }

    [Fact]
    public void AddOnce_dedupes_on_severity_and_message()
    {
        var report = new ImportReport();
        Assert.True(report.AddOnce(ImportSeverity.Warning, "a", "m"));
        Assert.False(report.AddOnce(ImportSeverity.Warning, "b", "m"));
        Assert.True(report.AddOnce(ImportSeverity.Skipped, "a", "m"));
        Assert.Equal(2, report.Entries.Count);
    }

    [Fact]
    public void AddRange_appends_entries()
    {
        var report = new ImportReport();
        report.AddRange([new(ImportSeverity.Info, "a", "m"), new(ImportSeverity.Skipped, "b", "n")]);
        Assert.Equal(2, report.Entries.Count);
    }

    [Fact]
    public void Join_drops_empty_parts()
    {
        Assert.Equal("a/b", ImportPath.Join("a", "", "b"));
        Assert.Equal("", ImportPath.Join());
        Assert.Equal("", ImportPath.Join("", ""));
    }

    [Fact]
    public void Bundle_counts_nested_requests_and_folders()
    {
        var tree = new ImportFolder(FolderDefinition.Create("root"),
        [
            new ImportRequest(HttpRequestDefinition.Create("r1")),
            new ImportFolder(FolderDefinition.Create("sub"),
            [
                new ImportRequest(HttpRequestDefinition.Create("r2")),
                new ImportFolder(FolderDefinition.Create("deep"), [new ImportRequest(HttpRequestDefinition.Create("r3"))]),
            ]),
        ]);
        var bundle = new ImportBundle(tree, [HttpRequestDefinition.Create("u1")], [], [], new ImportReport());
        Assert.Equal(4, bundle.RequestCount);
        Assert.Equal(3, bundle.FolderCount);
        var empty = ImportBundle.Empty(new ImportReport());
        Assert.Equal(0, empty.RequestCount);
        Assert.Equal(0, empty.FolderCount);
    }

    [Fact]
    public void Context_issues_sequential_ids()
    {
        var context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), context.NewId());
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), context.NewId());
        Assert.False(context.Options.IncludeStaticResources);
        Assert.NotEqual(Guid.Empty, ImportContext.Create().NewId());
    }

    [Fact]
    public void FromText_makes_one_file_and_Single_throws_when_empty()
    {
        var input = ImportInput.FromText("a.json", "{}");
        Assert.Equal("a.json", input.Name);
        Assert.Equal("{}", input.Single.Text);
        Assert.Throws<InvalidOperationException>(() => new ImportInput("x", []).Single);
    }
}
