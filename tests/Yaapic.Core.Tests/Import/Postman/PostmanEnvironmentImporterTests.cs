using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Postman;

public class PostmanEnvironmentImporterTests
{
    private static readonly PostmanEnvironmentImporter Importer = new();

    private static ImportBundle Run(string json) =>
        Importer.Import(ImportInput.FromText("env.json", json), new ImportContext(SequentialIds.Create(), ImportOptions.Default));

    [Fact]
    public void Sniff_tells_the_formats_apart()
    {
        Assert.Equal(ImportConfidence.Certain, Importer.Sniff(ImportInput.FromText("a", """{"_postman_variable_scope": "environment"}""")));
        Assert.Equal(ImportConfidence.Possible, Importer.Sniff(ImportInput.FromText("a", """{"name": "x", "values": []}""")));
        Assert.Equal(ImportConfidence.None, Importer.Sniff(ImportInput.FromText("a", """{"name": "x", "values": [], "item": []}""")));
        Assert.Equal(ImportConfidence.None, Importer.Sniff(ImportInput.FromText("a", """{"openapi": "3.0.0"}""")));
        Assert.Equal(ImportConfidence.None, Importer.Sniff(new ImportInput("a", [])));
    }

    [Fact]
    public void Environment_rows_keep_secret_and_disabled_flags()
    {
        var bundle = Run("""
            {"name": "Prod", "_postman_variable_scope": "environment", "values": [
              {"key": "host", "value": "a.test", "type": "default", "enabled": true},
              {"key": "token", "value": "abc", "type": "secret", "enabled": true},
              {"key": "old", "value": "x", "type": "any", "enabled": false}
            ]}
            """);

        var environment = Assert.Single(bundle.Environments);
        Assert.Equal("Prod", environment.Name);
        Assert.Null(environment.CollectionId);
        Assert.Equal(
            [new KeyValueEntry("host", "a.test"), new KeyValueEntry("token", "abc", true, true), new KeyValueEntry("old", "x", false)],
            environment.Variables);
        Assert.Null(bundle.Collection);
        Assert.Empty(bundle.UnfiledRequests);
        Assert.Empty(bundle.Globals);
    }

    [Fact]
    public void Globals_file_lands_in_globals()
    {
        var bundle = Run("""{"name": "Globals", "_postman_variable_scope": "globals", "values": [{"key": "a", "value": "1"}]}""");

        Assert.Empty(bundle.Environments);
        Assert.Equal([new KeyValueEntry("a", "1")], bundle.Globals);
    }

    [Fact]
    public void Missing_values_throws() =>
        Assert.Throws<ImportException>(() => Run("""{"name": "x"}"""));

    [Fact]
    public void Missing_name_falls_back_to_the_input_name() =>
        Assert.Equal("env.json", Assert.Single(Run("""{"values": []}""").Environments).Name);

    [Fact]
    public void Non_string_values_become_text_and_rows_without_key_are_dropped()
    {
        var bundle = Run("""{"name": "n", "values": [{"key": "n", "value": 1}, {"key": "b", "value": true}, {"value": "x"}, {"key": "", "value": "y"}, {"key": "z"}]}""");

        Assert.Equal(
            [new KeyValueEntry("n", "1"), new KeyValueEntry("b", "true"), new KeyValueEntry("z", "")],
            Assert.Single(bundle.Environments).Variables);
    }
}
