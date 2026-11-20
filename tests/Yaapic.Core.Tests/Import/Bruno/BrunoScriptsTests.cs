using Yaapic.Core.Import;
using Yaapic.Core.Import.Bruno;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Core.Tests.Import.Bruno;

public class BrunoScriptsTests
{
    private static (ScriptSet Set, ImportReport Report) Map(string bru)
    {
        ImportReport report = new();
        return (BrunoScripts.Map(BruParser.Parse(bru), "r", report), report);
    }

    private static ImportBundle Run(params (string Path, string Text)[] files)
    {
        var n = 0;
        ImportContext context = new(() => new Guid(++n, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]), ImportOptions.Default);
        return new BrunoImporter().Import(new ImportInput("picked", [.. files.Select(f => new ImportFile(f.Path, f.Text))]), context);
    }

    private const string Get = "get {\n  url: http://a.test\n}\n";

    [Fact]
    public void Combines_pre_request_vars_and_script()
    {
        var (set, report) = Map("""
            vars:pre-request {
              a: 1
              ~b: 2
              c: x"y
            }

            script:pre-request {
              bru.setVar("d", "e");
            }
            """);

        Assert.Equal(
            "yaapic.variables.set(\"a\", \"1\");\nyaapic.variables.set(\"c\", \"x\\\"y\");\n\nyaapic.variables.set(\"d\", \"e\");",
            set.PreRequest.Source);
        Assert.True(set.PostResponse.IsEmpty);
        Assert.Empty(report.Entries);
    }

    [Fact]
    public void Post_response_vars_are_expressions_and_tests_come_last()
    {
        var (set, _) = Map("""
            vars:post-response {
              id: res.body.id
            }

            script:post-response {
              bru.setVar("k", 1);
            }

            tests {
              test("ok", function() { expect(res.status).to.equal(200); });
            }
            """);

        var lines = set.PostResponse.Source.Split("\n\n");
        Assert.Equal(3, lines.Length);
        Assert.Equal("yaapic.variables.set(\"id\", yaapic.response.json().id);", lines[0]);
        Assert.StartsWith("yaapic.variables.set(\"k\"", lines[1]);
        Assert.StartsWith("yaapic.test(\"ok\"", lines[2]);
    }

    [Fact]
    public void Warns_about_an_unsupported_member_with_its_line()
    {
        var (_, report) = Map("script:pre-request {\n  bru.runRequest(\"x\");\n}\n");

        var w = Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Warning, w.Severity);
        Assert.Contains("runRequest", w.Message);
    }

    [Fact]
    public void Empty_file_gives_the_empty_set()
    {
        var (set, report) = Map(Get);

        Assert.Same(ScriptSet.Empty, set);
        Assert.Empty(report.Entries);
    }

    [Fact]
    public void Scripts_land_on_the_collection_folders_and_requests_with_one_review_warning()
    {
        var script = "script:pre-request {\n  bru.setVar(\"a\", 1);\n}\n";
        var bundle = Run(
            ("bruno.json", """{"name": "Shop"}"""),
            ("collection.bru", script),
            ("users/folder.bru", script),
            ("users/get.bru", "meta {\n  name: get\n}\n\n" + Get + "\n" + script));

        var root = bundle.Collection!;
        Assert.False(root.Folder.Scripts.PreRequest.IsEmpty);
        Assert.Empty(root.Folder.Variables);
        var users = Assert.IsType<ImportFolder>(Assert.Single(root.Children));
        Assert.False(users.Folder.Scripts.PreRequest.IsEmpty);
        var request = Assert.IsType<ImportRequest>(Assert.Single(users.Children)).Request;
        Assert.False(request.Scripts.PreRequest.IsEmpty);
        Assert.Single(bundle.Report.Entries, e => e.Severity == ImportSeverity.Warning && e.Message.Contains('3'));
    }
}
