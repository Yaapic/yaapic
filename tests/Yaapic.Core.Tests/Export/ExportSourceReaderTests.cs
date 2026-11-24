using Yaapic.Core.Export;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export;

public class ExportSourceReaderTests
{
    private sealed class FakeExporter(string id) : IExporter
    {
        public string Id { get; } = id;
        public string DisplayName => Id;
        public string FileExtension => ".x";
        public bool RequiresCollection => false;
        public ExportResult Export(ExportSource source) => new([], new ImportReport());
    }

    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryEnvironmentStore _envs = new();
    private readonly InMemoryGlobalsStore _globals = new();
    private readonly InMemoryCollectionStore _collections;
    private readonly ExportSourceReader _reader;

    public ExportSourceReaderTests()
    {
        _collections = new InMemoryCollectionStore(_requests);
        _reader = new ExportSourceReader(_collections, _envs, _globals);
    }

    private static readonly KeyValueEntry[] Rows = [new("a", "1"), new("s", "secret-value", true, true)];

    private async Task<FolderDefinition> AddFolder(string name, Guid? parent, params KeyValueEntry[] vars)
    {
        var f = FolderDefinition.Create(name) with { Variables = vars };
        await _collections.SaveFolderAsync(f, parent, TestContext.Current.CancellationToken);
        return f;
    }

    private Task<ExportSource> Read(Guid? id, Guid[]? envIds = null, bool globals = false, ImportReport? report = null) =>
        _reader.ReadAsync(id, envIds ?? [], globals, report ?? new ImportReport(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Collection_is_found_by_id_with_the_same_shape_and_blanked_secrets()
    {
        var top = await AddFolder("Top", null, Rows);
        var sub = await AddFolder("Sub", top.Id);
        var req = HttpRequestDefinition.Create("r1");
        await _requests.SaveAsync(req, TestContext.Current.CancellationToken);
        await _collections.MoveAsync(req.Id, sub.Id, TestContext.Current.CancellationToken);
        await AddFolder("Other", null);

        var source = await Read(top.Id);

        Assert.Equal("Top", source.Name);
        var c = source.Collection!;
        Assert.Equal(top.Id, c.Id);
        var s = Assert.Single(c.Children);
        Assert.Equal(sub.Id, s.Id);
        Assert.Equal(req.Id, Assert.Single(s.Children).Id);
        Assert.Equal("1", c.Folder!.Variables[0].Value);
        Assert.Equal(new KeyValueEntry("s", "", true, true), c.Folder.Variables[1]);
    }

    [Fact]
    public async Task Nested_or_unknown_folder_is_refused()
    {
        var top = await AddFolder("Top", null);
        var sub = await AddFolder("Sub", top.Id);
        await Assert.ThrowsAsync<ExportException>(() => Read(sub.Id));
        await Assert.ThrowsAsync<ExportException>(() => Read(Guid.NewGuid()));
    }

    [Fact]
    public async Task Environments_are_selected_by_id_and_blanked()
    {
        var e1 = new EnvironmentDefinition(Guid.NewGuid(), "e1", Rows);
        var e2 = new EnvironmentDefinition(Guid.NewGuid(), "e2", Rows);
        await _envs.SaveAsync(e1, TestContext.Current.CancellationToken);
        await _envs.SaveAsync(e2, TestContext.Current.CancellationToken);

        Assert.Empty((await Read(null)).Environments);
        var source = await Read(null, [e2.Id, Guid.NewGuid()]);

        Assert.Null(source.Collection);
        Assert.Equal("Environments", source.Name);
        var env = Assert.Single(source.Environments);
        Assert.Equal(e2.Id, env.Id);
        Assert.Equal("", env.Variables[1].Value);
        Assert.Equal("1", env.Variables[0].Value);
    }

    [Fact]
    public async Task Globals_are_included_only_on_request_and_blanked()
    {
        await _globals.SaveAsync(Rows, TestContext.Current.CancellationToken);
        Assert.Empty((await Read(null)).Globals);
        var g = (await Read(null, globals: true)).Globals;
        Assert.Equal("1", g[0].Value);
        Assert.Equal(new KeyValueEntry("s", "", true, true), g[1]);
    }

    [Fact]
    public async Task Blanked_secrets_are_counted_in_the_report()
    {
        var top = await AddFolder("Top", null, Rows);
        await _globals.SaveAsync(Rows, TestContext.Current.CancellationToken);
        var report = new ImportReport();
        await Read(top.Id, globals: true, report: report);
        var entry = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Info, entry.Severity);
        Assert.Contains("2", entry.Message);

        var clean = new ImportReport();
        await Read(null, report: clean);
        Assert.Empty(clean.Entries);
    }

    [Fact]
    public void Registry_keeps_order_and_finds_by_id()
    {
        var a = new FakeExporter("a");
        var b = new FakeExporter("b");
        var registry = new ExporterRegistry([a, b]);
        Assert.Equal([a, b], registry.Exporters);
        Assert.Same(b, registry.Find("b"));
        Assert.Null(registry.Find("zz"));
    }
}
