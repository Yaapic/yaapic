using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Export.Postman;
using Yaapic.Core.Import;
using Yaapic.Core.Import.Postman;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Export.Postman;

public class PostmanEnvironmentExporterTests
{
    private static readonly PostmanEnvironmentExporter Exporter = new();
    private static readonly Guid DevId = new("00000000-0000-0000-0000-0000000000d1");

    private static EnvironmentDefinition Env(string name, Guid? id = null, params KeyValueEntry[] rows) => new(id ?? Guid.NewGuid(), name, rows, null);

    private static ExportResult Run(IReadOnlyList<EnvironmentDefinition> environments, params KeyValueEntry[] globals) =>
        Exporter.Export(new ExportSource(null, environments, globals, "Environments"));

    private static string Text(ExportFile file) => Encoding.UTF8.GetString(file.Content);

    [Fact]
    public void Metadata()
    {
        Assert.Equal("postman-environment", Exporter.Id);
        Assert.Equal("Postman environment", Exporter.DisplayName);
        Assert.Equal(".json", Exporter.FileExtension);
        Assert.False(Exporter.RequiresCollection);
    }

    [Fact]
    public void WritesDefaultSecretAndDisabledRows()
    {
        var result = Run([Env("Dev Env", DevId, new KeyValueEntry("a", "1"), new KeyValueEntry("k", "", true, true), new KeyValueEntry("d", "x", false))]);
        var file = Assert.Single(result.Files);
        Assert.Equal("dev-env.postman_environment.json", file.Path);
        Assert.Equal("""
            {
              "id": "00000000-0000-0000-0000-0000000000d1",
              "name": "Dev Env",
              "values": [
                {
                  "key": "a",
                  "value": "1",
                  "type": "default",
                  "enabled": true
                },
                {
                  "key": "k",
                  "value": "",
                  "type": "secret",
                  "enabled": true
                },
                {
                  "key": "d",
                  "value": "x",
                  "type": "default",
                  "enabled": false
                }
              ],
              "_postman_variable_scope": "environment"
            }

            """.ReplaceLineEndings("\n"), Text(file));
        Assert.NotEqual(0xEF, file.Content[0]);
    }

    [Fact]
    public void GlobalsFileIsDeterministic()
    {
        var first = Assert.Single(Run([], new KeyValueEntry("g", "1")).Files);
        var second = Assert.Single(Run([], new KeyValueEntry("g", "1")).Files);
        Assert.Equal("globals.postman_globals.json", first.Path);
        Assert.Equal(Text(first), Text(second));
        var root = DataNodeJson.Parse(Text(first));
        Assert.Equal("Globals", root.GetString("name"));
        Assert.Equal("globals", root.GetString("_postman_variable_scope"));
        Assert.True(Guid.TryParse(root.GetString("id"), out _));
    }

    [Fact]
    public void EnvironmentsAndGlobalsGiveUniqueFiles()
    {
        var result = Run([Env("Dev"), Env("Dev")], new KeyValueEntry("g", "1"));
        Assert.Equal(
            ["dev.postman_environment.json", "dev.postman_environment-2.json", "globals.postman_globals.json"],
            result.Files.Select(f => f.Path));
    }

    [Fact]
    public void NothingToExportThrows() => Assert.Throws<ExportException>(() => Run([]));

    [Fact]
    public void RoundTripsThroughTheImporter()
    {
        var rows = new[] { new KeyValueEntry("a", "1"), new KeyValueEntry("k", "", true, true), new KeyValueEntry("d", "x", false) };
        var result = Run([Env("Dev", null, rows)], new KeyValueEntry("g", "2"));
        var importer = new PostmanEnvironmentImporter();
        ImportBundle Import(ExportFile f) =>
            importer.Import(ImportInput.FromText(f.Path, Text(f)), new ImportContext(SequentialIds.Create(), ImportOptions.Default));

        var env = Import(result.Files[0]).Environments[0];
        Assert.Equal("Dev", env.Name);
        Assert.Equal(rows, env.Variables);
        Assert.Equal([new KeyValueEntry("g", "2")], Import(result.Files[1]).Globals);
    }
}
