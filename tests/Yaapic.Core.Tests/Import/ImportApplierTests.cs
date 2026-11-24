using Yaapic.Core.Import;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import;

public class ImportApplierTests
{
    private readonly InMemoryRequestStore _requests = new();
    private readonly InMemoryEnvironmentStore _environments = new();
    private readonly InMemoryGlobalsStore _globals = new();
    private readonly InMemoryCollectionStore _collections;

    public ImportApplierTests() => _collections = new(_requests);

    private ImportApplier Applier(bool vaultOpen = true, IRequestStore? requests = null, ICollectionStore? collections = null) =>
        new(requests ?? _requests, collections ?? _collections, _environments, _globals, () => vaultOpen);

    private static ImportRequest Req(string name) => new(HttpRequestDefinition.Create(name));

    private static ImportBundle Bundle(ImportFolder? root = null, HttpRequestDefinition[]? unfiled = null,
        EnvironmentDefinition[]? environments = null, KeyValueEntry[]? globals = null, ImportReport? report = null) =>
        new(root, unfiled ?? [], environments ?? [], globals ?? [], report ?? new ImportReport());

    private static FolderDefinition Folder(string name, params KeyValueEntry[] variables) =>
        FolderDefinition.Create(name) with { Variables = variables };

    [Fact]
    public async Task A_nested_bundle_lands_with_the_right_parents()
    {
        var tree = new ImportFolder(Folder("API"),
        [
            Req("top"),
            new ImportFolder(Folder("Users"), [Req("list"), new ImportFolder(Folder("Deep"), [Req("leaf")])]),
        ]);

        var outcome = await Applier().ApplyAsync(Bundle(tree), TestContext.Current.CancellationToken);

        var roots = await _collections.LoadTreeAsync(TestContext.Current.CancellationToken);
        var api = Assert.Single(roots);
        Assert.Equal("API", api.Name);
        Assert.Equal(["Users", "top"], api.Children.Select(c => c.Name));
        var users = api.Children[0];
        Assert.Equal(["Deep", "list"], users.Children.Select(c => c.Name));
        Assert.Equal("leaf", Assert.Single(users.Children[0].Children).Name);
        Assert.Equal(3, outcome.Folders);
        Assert.Equal(3, outcome.Requests);
    }

    [Fact]
    public async Task Unfiled_requests_stay_at_the_root()
    {
        var outcome = await Applier().ApplyAsync(Bundle(unfiled: [HttpRequestDefinition.Create("solo")]),
            TestContext.Current.CancellationToken);

        var roots = await _collections.LoadTreeAsync(TestContext.Current.CancellationToken);
        Assert.Equal("solo", Assert.Single(roots).Name);
        Assert.Equal(1, outcome.Requests);
        Assert.Equal(0, outcome.Folders);
    }

    [Fact]
    public async Task A_name_clash_is_renamed_ignoring_case_and_reported()
    {
        var ct = TestContext.Current.CancellationToken;
        await _collections.SaveFolderAsync(FolderDefinition.Create("api"), null, ct);
        await _collections.SaveFolderAsync(FolderDefinition.Create("API (2)"), null, ct);

        var outcome = await Applier().ApplyAsync(Bundle(new ImportFolder(Folder("API"), [])), ct);

        var roots = await _collections.LoadTreeAsync(ct);
        Assert.Contains(roots, r => r.Name == "API (3)");
        var info = Assert.Single(outcome.Report.Entries);
        Assert.Equal(ImportSeverity.Info, info.Severity);
        Assert.Contains("API (3)", info.Message);
    }

    [Fact]
    public async Task An_environment_clash_is_renamed()
    {
        var ct = TestContext.Current.CancellationToken;
        await _environments.SaveAsync(EnvironmentDefinition.Create("Dev"), ct);

        var outcome = await Applier().ApplyAsync(Bundle(environments: [EnvironmentDefinition.Create("dev")]), ct);

        Assert.Equal(["Dev", "dev (2)"], (await _environments.ListAsync(ct)).Select(e => e.Name).Order());
        Assert.Equal(1, outcome.Environments);
        Assert.Equal(ImportSeverity.Info, Assert.Single(outcome.Report.Entries).Severity);
    }

    [Fact]
    public async Task Globals_keep_existing_keys_and_add_new_ones()
    {
        var ct = TestContext.Current.CancellationToken;
        await _globals.SaveAsync([new("a", "old")], ct);

        var outcome = await Applier().ApplyAsync(Bundle(globals: [new("a", "new"), new("b", "1"), new("A", "2")]), ct);

        Assert.Equal(["a=old", "b=1", "A=2"], (await _globals.LoadAsync(ct)).Select(g => $"{g.Key}={g.Value}"));
        Assert.Equal(2, outcome.GlobalVariables);
        var info = Assert.Single(outcome.Report.Entries);
        Assert.Equal(ImportSeverity.Info, info.Severity);
        Assert.Contains('1', info.Message);
    }

    [Fact]
    public async Task Globals_are_not_saved_when_nothing_is_new()
    {
        var ct = TestContext.Current.CancellationToken;
        await _globals.SaveAsync([new("a", "old")], ct);
        var before = _globals.SaveCount;

        await Applier().ApplyAsync(Bundle(globals: [new("a", "new")]), ct);

        Assert.Equal(before, _globals.SaveCount);
    }

    [Fact]
    public async Task A_locked_vault_drops_secret_values_with_one_warning()
    {
        var ct = TestContext.Current.CancellationToken;
        var secret = new KeyValueEntry("token", "s3", true, true);
        var tree = new ImportFolder(Folder("API", secret, new("plain", "p")), []);
        var env = EnvironmentDefinition.Create("E") with { Variables = [secret] };

        var outcome = await Applier(vaultOpen: false).ApplyAsync(
            Bundle(tree, environments: [env], globals: [secret with { Key = "g" }]), ct);

        var folder = Assert.Single(await _collections.LoadTreeAsync(ct)).Folder!;
        Assert.Equal([new("token", "", true, false), new("plain", "p")], folder.Variables);
        var saved = Assert.Single(await _environments.ListAsync(ct));
        Assert.Equal([new("token", "", true, false)], saved.Variables);
        Assert.Equal([new("g", "", true, false)], await _globals.LoadAsync(ct));
        var warning = Assert.Single(outcome.Report.Entries);
        Assert.Equal(ImportSeverity.Warning, warning.Severity);
        Assert.Contains('3', warning.Message);
    }

    [Fact]
    public async Task An_unlocked_vault_passes_secret_rows_through()
    {
        var ct = TestContext.Current.CancellationToken;
        var secret = new KeyValueEntry("token", "s3", true, true);
        var env = EnvironmentDefinition.Create("E") with { Variables = [secret] };

        var outcome = await Applier().ApplyAsync(
            Bundle(new ImportFolder(Folder("API", secret), []), environments: [env], globals: [secret]), ct);

        Assert.Equal([secret], Assert.Single(await _collections.LoadTreeAsync(ct)).Folder!.Variables);
        Assert.Equal([secret], Assert.Single(await _environments.ListAsync(ct)).Variables);
        Assert.Equal([secret], await _globals.LoadAsync(ct));
        Assert.Empty(outcome.Report.Entries);
    }

    [Fact]
    public async Task Nested_folder_secret_rows_are_always_blanked()
    {
        var ct = TestContext.Current.CancellationToken;
        var secret = new KeyValueEntry("token", "s3", true, true);
        var tree = new ImportFolder(Folder("API"), [new ImportFolder(Folder("Sub", secret), [])]);

        var outcome = await Applier().ApplyAsync(Bundle(tree), ct);

        var sub = Assert.Single(Assert.Single(await _collections.LoadTreeAsync(ct)).Children).Folder!;
        Assert.Equal([new("token", "", true, false)], sub.Variables);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(outcome.Report.Entries).Severity);
    }

    [Fact]
    public async Task A_failing_request_is_skipped_and_the_others_land()
    {
        var ct = TestContext.Current.CancellationToken;
        var tree = new ImportFolder(Folder("API"), [Req("a"), Req("bad"), Req("c")]);

        var outcome = await Applier(requests: new FailingRequests(_requests, "bad")).ApplyAsync(Bundle(tree), ct);

        var children = Assert.Single(await _collections.LoadTreeAsync(ct)).Children;
        Assert.Equal(["a", "c"], children.Select(c => c.Name));
        Assert.Equal(2, outcome.Requests);
        var skipped = Assert.Single(outcome.Report.Entries);
        Assert.Equal(ImportSeverity.Skipped, skipped.Severity);
        Assert.Equal("API/bad", skipped.Path);
    }

    [Fact]
    public async Task A_failing_folder_skips_its_subtree_with_a_single_entry()
    {
        var ct = TestContext.Current.CancellationToken;
        var tree = new ImportFolder(Folder("API"),
        [
            Req("ok"),
            new ImportFolder(Folder("Bad"), [Req("x"), new ImportFolder(Folder("In"), [Req("y")])]),
        ]);

        var outcome = await Applier(collections: new FailingFolders(_collections, "Bad")).ApplyAsync(Bundle(tree), ct);

        Assert.Equal(1, outcome.Folders);
        Assert.Equal(1, outcome.Requests);
        var skipped = Assert.Single(outcome.Report.Entries);
        Assert.Equal(ImportSeverity.Skipped, skipped.Severity);
        Assert.Equal("API/Bad", skipped.Path);
        Assert.Contains('2', skipped.Message);
    }

    [Fact]
    public async Task Cancellation_throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Applier().ApplyAsync(Bundle(unfiled: [HttpRequestDefinition.Create("a")]), cts.Token));
    }

    [Fact]
    public async Task The_outcome_counts_and_puts_the_bundle_entries_first()
    {
        var ct = TestContext.Current.CancellationToken;
        var report = new ImportReport();
        report.Warning("src", "from the importer");
        await _environments.SaveAsync(EnvironmentDefinition.Create("E"), ct);

        var outcome = await Applier().ApplyAsync(
            Bundle(new ImportFolder(Folder("API"), [Req("r")]), [HttpRequestDefinition.Create("u")],
                [EnvironmentDefinition.Create("E")], [new("k", "v")], report), ct);

        Assert.Equal((1, 2, 1, 1), (outcome.Folders, outcome.Requests, outcome.Environments, outcome.GlobalVariables));
        Assert.Equal("from the importer", outcome.Report.Entries[0].Message);
        Assert.Equal(2, outcome.Report.Entries.Count);
        Assert.NotSame(report, outcome.Report);
        Assert.Single(report.Entries);
    }

    private sealed class FailingRequests(IRequestStore inner, string name) : IRequestStore
    {
        public Task<IReadOnlyList<HttpRequestDefinition>> ListAsync(CancellationToken ct) => inner.ListAsync(ct);

        public Task SaveAsync(HttpRequestDefinition request, CancellationToken ct) =>
            request.Name == name ? throw new StorageException("disk full") : inner.SaveAsync(request, ct);

        public Task DeleteAsync(Guid id, CancellationToken ct) => inner.DeleteAsync(id, ct);
    }

    private sealed class FailingFolders(ICollectionStore inner, string name) : ICollectionStore
    {
        public Task<IReadOnlyList<CollectionNode>> LoadTreeAsync(CancellationToken ct) => inner.LoadTreeAsync(ct);

        public Task SaveFolderAsync(FolderDefinition folder, Guid? parentId, CancellationToken ct) =>
            folder.Name == name ? throw new StorageException("disk full") : inner.SaveFolderAsync(folder, parentId, ct);

        public Task DeleteFolderAsync(Guid id, CancellationToken ct) => inner.DeleteFolderAsync(id, ct);

        public Task MoveAsync(Guid id, Guid? newParentId, CancellationToken ct) => inner.MoveAsync(id, newParentId, ct);
    }
}
