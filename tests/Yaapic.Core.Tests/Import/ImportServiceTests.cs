using Yaapic.Core.Import;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import;

public class ImportServiceTests
{
    private sealed class FakeImporter(string id, ImportConfidence confidence, Func<ImportContext, ImportBundle>? import = null, bool sniffThrows = false) : IImporter
    {
        public string Id { get; } = id;

        public string DisplayName => "Fake " + Id;

        public ImportConfidence Sniff(ImportInput input) => sniffThrows ? throw new InvalidOperationException() : confidence;

        public ImportBundle Import(ImportInput input, ImportContext context) =>
            import is null ? WithRequests(1, context.Report) : import(context);
    }

    private static readonly ImportInput Input = ImportInput.FromText("a.json", "{}");

    private static ImportBundle WithRequests(int count, ImportReport? report = null) =>
        new(null, [.. Enumerable.Range(0, count).Select(i => HttpRequestDefinition.Create("r" + i))], [], [], report ?? new ImportReport());

    private static ImportService Service(params IImporter[] importers)
    {
        var requests = new InMemoryRequestStore();
        var applier = new ImportApplier(requests, new InMemoryCollectionStore(requests), new InMemoryEnvironmentStore(), new InMemoryGlobalsStore(), () => true);
        return new ImportService(new ImporterRegistry(importers), applier);
    }

    [Fact]
    public void Detect_puts_certain_first_then_registration_order_and_ignores_none_and_throwing_sniffs()
    {
        var registry = new ImporterRegistry(
        [
            new FakeImporter("p1", ImportConfidence.Possible),
            new FakeImporter("none", ImportConfidence.None),
            new FakeImporter("boom", ImportConfidence.Certain, sniffThrows: true),
            new FakeImporter("c1", ImportConfidence.Certain),
            new FakeImporter("p2", ImportConfidence.Possible),
            new FakeImporter("c2", ImportConfidence.Certain),
        ]);

        Assert.Equal(["c1", "c2", "p1", "p2"], registry.Detect(Input).Select(i => i.Id));
        Assert.Equal(6, registry.Importers.Count);
        Assert.Equal("p2", registry.Find("p2")?.Id);
        Assert.Null(registry.Find("nope"));
    }

    [Fact]
    public void Preview_uses_the_explicit_importer_or_the_detected_one()
    {
        var service = Service(new FakeImporter("a", ImportConfidence.None), new FakeImporter("b", ImportConfidence.Possible));

        Assert.Equal("a", service.Preview(Input, "a", ImportOptions.Default).Importer.Id);
        Assert.Equal("b", service.Preview(Input, null, ImportOptions.Default).Importer.Id);
        Assert.Throws<ArgumentException>(() => service.Preview(Input, "x", ImportOptions.Default));
    }

    [Fact]
    public void Preview_throws_when_nothing_is_recognized()
    {
        var service = Service(new FakeImporter("a", ImportConfidence.None));

        Assert.Throws<ImportException>(() => service.Preview(Input, null, ImportOptions.Default));
    }

    [Fact]
    public void A_failing_importer_becomes_an_ImportException_naming_it()
    {
        var service = Service(new FakeImporter("a", ImportConfidence.Certain, _ => throw new FormatException("bad")));

        var ex = Assert.Throws<ImportException>(() => service.Preview(Input, null, ImportOptions.Default));

        Assert.Contains("Fake a", ex.Message);
        Assert.Contains("bad", ex.Message);
        Assert.IsType<FormatException>(ex.InnerException);
    }

    [Fact]
    public void ImportException_and_cancellation_pass_through()
    {
        var original = new ImportException("mine");
        var service = Service(
            new FakeImporter("e", ImportConfidence.Certain, _ => throw original),
            new FakeImporter("c", ImportConfidence.Certain, _ => throw new OperationCanceledException()));

        Assert.Same(original, Assert.Throws<ImportException>(() => service.Preview(Input, "e", ImportOptions.Default)));
        Assert.Throws<OperationCanceledException>(() => service.Preview(Input, "c", ImportOptions.Default));
    }

    [Fact]
    public void More_than_20000_requests_throw_and_exactly_20000_pass()
    {
        var service = Service(
            new FakeImporter("big", ImportConfidence.Certain, c => WithRequests(20001, c.Report)),
            new FakeImporter("max", ImportConfidence.Certain, c => WithRequests(20000, c.Report)));

        Assert.Throws<ImportException>(() => service.Preview(Input, "big", ImportOptions.Default));
        Assert.Equal(20000, service.Preview(Input, "max", ImportOptions.Default).Bundle.RequestCount);
    }

    [Fact]
    public void An_empty_bundle_gets_one_warning()
    {
        var service = Service(new FakeImporter("a", ImportConfidence.Certain, c => ImportBundle.Empty(c.Report)));

        var report = service.Preview(Input, null, ImportOptions.Default).Bundle.Report;

        var entry = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Contains("Fake a", entry.Message);
    }

    [Fact]
    public async Task ApplyAsync_writes_through_the_stores()
    {
        var service = Service(new FakeImporter("a", ImportConfidence.Certain));

        var preview = service.Preview(Input, null, ImportOptions.Default);
        var outcome = await service.ApplyAsync(preview, TestContext.Current.CancellationToken);

        Assert.Equal(1, outcome.Requests);
    }
}
