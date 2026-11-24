using Yaapic.Core.Import;
using Yaapic.Core.Import.Bruno;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Tests.Import.Bruno;

public class BrunoImporterTests
{
    private static string Req(string name, string extra = "", string auth = "none", string verbBlock = "") => $$"""
        meta {
          name: {{name}}
          type: http
          seq: 1
        }

        get {
          url: http://a.test/{{name}}
          body: none
          auth: {{auth}}
        }
        {{verbBlock}}
        {{extra}}
        """;

    private static ImportBundle Run(params (string Path, string Text)[] files)
    {
        var n = 0;
        ImportContext context = new(() => new Guid(++n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]), ImportOptions.Default);
        return new BrunoImporter().Import(new ImportInput("picked", [.. files.Select(f => new ImportFile(f.Path, f.Text))]), context);
    }

    private static IEnumerable<HttpRequestDefinition> Requests(ImportItem item) => item switch
    {
        ImportRequest r => [r.Request],
        ImportFolder f => f.Children.SelectMany(Requests),
        _ => [],
    };

    private static HttpRequestDefinition Find(ImportBundle bundle, string name) =>
        Requests(bundle.Collection!).Single(r => r.Name == name);

    [Fact]
    public void Imports_nested_folders_with_names_from_folder_files()
    {
        var bundle = Run(
            ("bruno.json", """{"name": "Shop", "version": "1", "type": "collection"}"""),
            ("users/folder.bru", "meta {\n  name: People\n  seq: 2\n}\n"),
            ("users/admins/list.bru", Req("list")),
            ("users/get.bru", Req("get")),
            ("root.bru", Req("root")));

        var root = bundle.Collection!;
        Assert.Equal("Shop", root.Folder.Name);
        var people = Assert.IsType<ImportFolder>(root.Children.Single(c => c is ImportFolder));
        Assert.Equal("People", people.Folder.Name);
        Assert.Equal("admins", Assert.IsType<ImportFolder>(people.Children.Single(c => c is ImportFolder)).Folder.Name);
        Assert.Equal(3, bundle.RequestCount);
        Assert.Equal(3, bundle.FolderCount);
    }

    [Fact]
    public void Omits_directories_without_requests_and_honours_ignore()
    {
        var bundle = Run(
            ("bruno.json", """{"name": "C", "ignore": ["skipme"]}"""),
            ("empty/folder.bru", "meta {\n  name: Empty\n}\n"),
            ("skipme/a.bru", Req("a")),
            ("node_modules/b.bru", Req("b")),
            ("environments/local.bru", "vars {\n  a: 1\n}\n"),
            ("keep/c.bru", Req("c")));

        Assert.Equal(1, bundle.RequestCount);
        Assert.Equal(2, bundle.FolderCount);
    }

    [Fact]
    public void Uses_the_input_name_without_bruno_json_and_the_invalid_json_warning()
    {
        Assert.Equal("picked", Run(("a.bru", Req("a")), ("b.bru", Req("b"))).Collection!.Folder.Name);

        var bundle = Run(("bruno.json", "{nope"), ("a.bru", Req("a")));
        Assert.Equal("picked", bundle.Collection!.Folder.Name);
        Assert.Equal(1, bundle.Report.Count(ImportSeverity.Warning));
    }

    [Fact]
    public void Copies_collection_and_folder_headers_with_replacement()
    {
        var bundle = Run(
            ("bruno.json", """{"name": "C"}"""),
            ("collection.bru", "headers {\n  X-Root: r\n  X-Both: root\n}\n"),
            ("f/folder.bru", "headers {\n  x-both: folder\n  X-Folder: f\n}\n"),
            ("f/a.bru", Req("a", "headers {\n  X-Own: o\n  X-FOLDER: own\n}\n")),
            ("top.bru", Req("top")));

        Assert.Equal(
            ["X-Root=r", "x-both=folder", "X-Own=o", "X-FOLDER=own"],
            Find(bundle, "a").Headers.Select(h => $"{h.Key}={h.Value}"));
        Assert.Equal(["X-Root", "X-Both"], Find(bundle, "top").Headers.Select(h => h.Key));
        Assert.Contains(bundle.Report.Entries, e => e.Message == Strings.ImportBrunoHeadersInherited(2));
    }

    [Fact]
    public void Resolves_inherited_auth_from_the_nearest_ancestor()
    {
        var bundle = Run(
            ("bruno.json", """{"name": "C"}"""),
            ("collection.bru", "auth {\n  mode: bearer\n}\n\nauth:bearer {\n  token: root\n}\n"),
            ("deep/folder.bru", "auth {\n  mode: basic\n}\n\nauth:basic {\n  username: u\n  password: p\n}\n"),
            ("deep/a.bru", Req("a", auth: "inherit")),
            ("deep/own.bru", Req("own", "auth:bearer {\n  token: mine\n}\n", "bearer")),
            ("transparent/folder.bru", "auth {\n  mode: inherit\n}\n"),
            ("transparent/b.bru", Req("b", auth: "inherit")));

        Assert.Equal(AuthKind.Basic, Find(bundle, "a").Auth.Kind);
        Assert.Equal("u", Find(bundle, "a").Auth.Username);
        Assert.Equal("mine", Find(bundle, "own").Auth.Token);
        Assert.Equal(AuthKind.Bearer, Find(bundle, "b").Auth.Kind);
        Assert.Equal("root", Find(bundle, "b").Auth.Token);
        Assert.Contains(bundle.Report.Entries, e => e.Message == Strings.ImportAuthInherited(2));
    }

    [Fact]
    public void Inherit_without_any_ancestor_auth_is_none()
    {
        var bundle = Run(("a.bru", Req("a", auth: "inherit")), ("b.bru", Req("b")));
        Assert.Equal(RequestAuth.None, Find(bundle, "a").Auth);
    }

    [Fact]
    public void Skips_an_unparsable_request_and_keeps_the_rest()
    {
        var bundle = Run(("bad.bru", "meta {\n  name: bad\n}\n"), ("good.bru", Req("good")));

        Assert.Equal(1, bundle.RequestCount);
        Assert.Equal(1, bundle.Report.Count(ImportSeverity.Skipped));
    }

    [Fact]
    public void A_single_bru_file_imports_as_one_unfiled_request()
    {
        var bundle = Run(("solo.bru", Req("solo")));

        Assert.Null(bundle.Collection);
        Assert.Equal("solo", Assert.Single(bundle.UnfiledRequests).Name);
    }

    [Fact]
    public void Reports_that_the_order_is_lost_once()
    {
        var bundle = Run(("a.bru", Req("a")), ("b.bru", Req("b")));

        Assert.Single(bundle.Report.Entries, e => e.Message == Strings.ImportOrderLost);
    }

    [Fact]
    public void Sniffs_by_manifest_and_bru_count()
    {
        BrunoImporter importer = new();
        ImportInput Input(params string[] paths) => new("x", [.. paths.Select(p => new ImportFile(p, ""))]);

        Assert.Equal(ImportConfidence.Certain, importer.Sniff(Input("sub/bruno.json")));
        Assert.Equal(ImportConfidence.Certain, importer.Sniff(Input("a.bru", "b.bru")));
        Assert.Equal(ImportConfidence.Possible, importer.Sniff(Input("a.bru")));
        Assert.Equal(ImportConfidence.None, importer.Sniff(Input("a.json")));
        Assert.Equal("bruno", importer.Id);
    }

    [Fact]
    public void Imports_environments_scoped_to_the_collection()
    {
        var bundle = Run(
            ("bruno.json", """{"name": "Shop"}"""),
            ("a.bru", Req("a")),
            ("environments/Local.bru", "vars {\r\n  host: http://l.test\r\n  ~off: 1\r\n  keep: {{process.env.X}}\r\n}\r\nvars:secret [\r\n  token,\r\n  keep\r\n]\r\n"),
            ("environments/Empty.bru", ""),
            ("environments/sub/Ignored.bru", "vars {\n  a: 1\n}\n"));

        Assert.Equal(["Local", "Empty"], bundle.Environments.Select(e => e.Name));
        Assert.All(bundle.Environments, e => Assert.Equal(bundle.Collection!.Folder.Id, e.CollectionId));
        Assert.Equal(
            [
                new KeyValueEntry("host", "http://l.test"),
                new KeyValueEntry("off", "1", false),
                new KeyValueEntry("keep", "", true, true),
                new KeyValueEntry("token", "", true, true),
            ],
            bundle.Environments[0].Variables);
        Assert.Empty(bundle.Environments[1].Variables);
        Assert.Single(bundle.Report.Entries, e => e.Message == Strings.ImportBrunoSecretsEmpty(2));
        Assert.Equal(1, bundle.RequestCount);
    }

    [Fact]
    public void A_collection_without_environments_directory_has_none()
    {
        Assert.Empty(Run(("bruno.json", "{}"), ("a.bru", Req("a"))).Environments);
    }

    [Fact]
    public void Warns_once_about_process_env_in_environment_values()
    {
        var bundle = Run(
            ("bruno.json", "{}"),
            ("environments/A.bru", "vars {\n  k: {{process.env.X}}\n}\n"));

        Assert.Single(bundle.Report.Entries, e => e.Message == Strings.ImportBrunoProcessEnv);
    }
}
