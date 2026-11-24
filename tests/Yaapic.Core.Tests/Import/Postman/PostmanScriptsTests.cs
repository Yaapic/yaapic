using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Postman;

public class PostmanScriptsTests
{
    private static ImportBundle Run(string json, out ImportContext context)
    {
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return new PostmanCollectionImporter().Import(ImportInput.FromText("c.json", json), context);
    }

    private static string Event(string listen, string exec, string extra = "") =>
        "{\"listen\": \"" + listen + "\", " + extra + " \"script\": {\"type\": \"text/javascript\", \"exec\": " + exec + "}}";

    private static string Coll(string events = "[]", string variables = "[]", string items = "[]") =>
        $$"""{"info": {"name": "C"}, "event": {{events}}, "variable": {{variables}}, "item": {{items}}}""";

    [Fact]
    public void Events_land_on_the_collection_folder_and_request()
    {
        var bundle = Run(Coll(
            $"[{Event("prerequest", "[\"a()\"]")},{Event("test", "\"b()\"")}]",
            items: "[{\"name\": \"f\", \"event\": [" + Event("test", "[\"c()\"]") + "], \"item\": [{\"name\": \"r\", \"event\": [" + Event("prerequest", "[\"d()\"]") + "], \"request\": {\"url\": \"u\"}}]}]"),
            out _);

        var root = bundle.Collection!;
        Assert.Equal("a()", root.Folder.Scripts.PreRequest.Source);
        Assert.Equal("b()", root.Folder.Scripts.PostResponse.Source);
        var folder = Assert.IsType<ImportFolder>(Assert.Single(root.Children));
        Assert.Equal("c()", folder.Folder.Scripts.PostResponse.Source);
        var request = Assert.IsType<ImportRequest>(Assert.Single(folder.Children)).Request;
        Assert.Equal("d()", request.Scripts.PreRequest.Source);
        Assert.True(request.Scripts.PostResponse.IsEmpty);
    }

    [Fact]
    public void Exec_array_is_joined_with_newlines_and_same_listen_events_with_a_blank_line()
    {
        var bundle = Run(Coll($"[{Event("prerequest", "[\"x\", \"y\"]")},{Event("prerequest", "\"z\"")}]"), out _);

        Assert.Equal("x\ny\n\nz", bundle.Collection!.Folder.Scripts.PreRequest.Source);
    }

    [Fact]
    public void Disabled_events_and_other_listens_are_ignored()
    {
        var bundle = Run(Coll($"[{Event("prerequest", "\"a\"", "\"disabled\": true,")},{Event("other", "\"b\"")}]"), out var context);

        Assert.True(bundle.Collection!.Folder.Scripts.IsEmpty);
        Assert.Empty(context.Report.Entries);
    }

    [Fact]
    public void Non_javascript_type_is_skipped_with_an_entry()
    {
        var bundle = Run(Coll("""[{"listen": "test", "script": {"type": "text/x-lua", "exec": ["a"]}}]"""), out var context);

        Assert.True(bundle.Collection!.Folder.Scripts.IsEmpty);
        Assert.Equal(1, context.Report.Count(ImportSeverity.Skipped));
    }

    [Fact]
    public void Postman_calls_are_converted_and_unsupported_members_warn_with_line()
    {
        var bundle = Run(Coll($"[{Event("prerequest", "[\"pm.environment.set('a', 1);\", \"pm.execution.skipRequest();\"]")}]"), out var context);

        Assert.Contains("yaapic.environment.set(", bundle.Collection!.Folder.Scripts.PreRequest.Source, StringComparison.Ordinal);
        var warnings = context.Report.Entries.Where(e => e.Severity == ImportSeverity.Warning).ToList();
        Assert.Contains(warnings, e => e.Message.Contains("skipRequest", StringComparison.Ordinal) && e.Message.Contains("line 2", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Review_warning_appears_once_and_empty_scripts_add_nothing()
    {
        var bundle = Run(Coll(
            $"[{Event("prerequest", "[\"a()\"]")}]",
            items: "[{\"name\": \"r\", \"event\": [" + Event("test", "[\"b()\"]") + ", " + Event("prerequest", "[\" \"]") + "], \"request\": {\"url\": \"u\"}}]"),
            out var context);

        Assert.Single(bundle.Collection!.Children);
        Assert.Contains(context.Report.Entries, e => e.Message.Contains("2 scripts", StringComparison.Ordinal));
        Assert.Equal(1, context.Report.Count(ImportSeverity.Warning));

        Run(Coll($"[{Event("prerequest", "[\"\"]")}]"), out var empty);
        Assert.Empty(empty.Report.Entries);
    }

    [Fact]
    public void Collection_variables_keep_disabled_null_numeric_and_secret()
    {
        var bundle = Run(Coll(variables: """
            [{"key": "a", "value": "1"}, {"key": "b", "value": "2", "disabled": true}, {"key": "c", "value": null},
             {"key": "d", "value": 42}, {"key": "s", "value": "x", "type": "secret"}, {"value": "nokey"}]
            """), out _);

        Assert.Equal(
            [
                new KeyValueEntry("a", "1"),
                new KeyValueEntry("b", "2", false),
                new KeyValueEntry("c", ""),
                new KeyValueEntry("d", "42"),
                new KeyValueEntry("s", "x", true, true),
            ],
            bundle.Collection!.Folder.Variables);
    }

    [Fact]
    public void Nested_folder_variables_are_kept_without_the_secret_flag()
    {
        var bundle = Run(Coll(items: """[{"name": "f", "variable": [{"key": "k", "value": "v", "type": "secret"}], "item": []}]"""), out _);

        var folder = Assert.IsType<ImportFolder>(Assert.Single(bundle.Collection!.Children)).Folder;
        Assert.Equal([new KeyValueEntry("k", "v")], folder.Variables);
    }
}
