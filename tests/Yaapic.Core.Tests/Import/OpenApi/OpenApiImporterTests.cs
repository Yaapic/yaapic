using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.OpenApi;

public class OpenApiImporterTests
{
    private sealed class FakeYaml(DataNode tree) : IYamlTreeReader
    {
        public string? Received { get; private set; }

        public DataNode Parse(string yaml)
        {
            Received = yaml;
            return tree;
        }
    }

    private static readonly OpenApiImporter Importer = new(new StructuredTextReader(new FakeYaml(DataNode.Null)));

    private static ImportBundle Run(string text, out ImportContext context, OpenApiImporter? importer = null)
    {
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return (importer ?? Importer).Import(ImportInput.FromText("api.json", text), context);
    }

    [Fact]
    public void Identity()
    {
        Assert.Equal("openapi", Importer.Id);
        Assert.Equal("OpenAPI", Importer.DisplayName);
    }

    [Theory]
    [InlineData("{\n  \"openapi\": \"3.0.3\",\n  \"paths\": {}\n}", ImportConfidence.Certain)]
    [InlineData("{\"openapi\":\"3.1.0\",\"paths\":{}}", ImportConfidence.Certain)]
    [InlineData("openapi: 3.0.0\npaths: {}", ImportConfidence.Certain)]
    [InlineData("openapi: '3.1.0'\n", ImportConfidence.Certain)]
    [InlineData("{\n \"swagger\": \"2.0\"\n}", ImportConfidence.Certain)]
    [InlineData("swagger: \"2.0\"\n", ImportConfidence.Certain)]
    [InlineData("{\"paths\": {}}", ImportConfidence.Possible)]
    [InlineData("info:\n  title: x\npaths:\n  /a: {}", ImportConfidence.Possible)]
    [InlineData("{\"info\": {\"title\": \"x\"}}", ImportConfidence.None)]
    [InlineData("openapi: 4.0.0", ImportConfidence.None)]
    public void Sniff_cases(string text, ImportConfidence expected) =>
        Assert.Equal(expected, Importer.Sniff(ImportInput.FromText("a", text)));

    [Fact]
    public void Sniff_without_file_is_none() => Assert.Equal(ImportConfidence.None, Importer.Sniff(new ImportInput("a", [])));

    [Fact]
    public void Json_document_names_the_collection_from_the_title()
    {
        var bundle = Run("""{"openapi":"3.0.3","info":{"title":"Pets\nAPI","description":"d"},"servers":[{"url":"https://a"}],"paths":{}}""", out var context);
        var folder = Assert.IsType<ImportFolder>(bundle.Collection).Folder;
        Assert.Equal("Pets API", folder.Name);
        Assert.Equal("d", folder.Description);
        Assert.Empty(context.Report.Entries);
    }

    [Fact]
    public void Title_falls_back_to_the_input_name()
    {
        var bundle = Run("""{"openapi":"3.1.0","paths":{}}""", out _);
        Assert.Equal("api.json", Assert.IsType<ImportFolder>(bundle.Collection).Folder.Name);
    }

    [Fact]
    public void Yaml_input_goes_through_the_yaml_reader()
    {
        var tree = DataNodeJson.Parse("""{"openapi":"3.0.0","info":{"title":"Y"},"paths":{}}""");
        var yaml = new FakeYaml(tree);
        var bundle = Run("openapi: 3.0.0\npaths: {}\n", out _, new OpenApiImporter(new StructuredTextReader(yaml)));
        Assert.Equal("openapi: 3.0.0\npaths: {}\n", yaml.Received);
        Assert.Equal("Y", Assert.IsType<ImportFolder>(bundle.Collection).Folder.Name);
    }

    [Fact]
    public void Swagger_2_is_accepted()
    {
        var bundle = Run("""{"swagger":"2.0","info":{"title":"S"},"paths":{}}""", out _);
        Assert.Equal("S", Assert.IsType<ImportFolder>(bundle.Collection).Folder.Name);
    }

    [Fact]
    public void Other_3_x_version_is_accepted_with_an_info()
    {
        Run("""{"openapi":"3.2.0","servers":[{"url":"https://a"}],"paths":{}}""", out var context);
        Assert.Equal(ImportSeverity.Info, Assert.Single(context.Report.Entries).Severity);
    }

    [Theory]
    [InlineData("""{"openapi":"3.0.0"}""")]
    [InlineData("""{"openapi":"3.0.0","paths":[]}""")]
    [InlineData("""{"paths":{}}""")]
    [InlineData("[1]")]
    [InlineData("{bad")]
    public void Not_a_document_throws(string text) => Assert.Throws<ImportException>(() => Run(text, out _));

    [Theory]
    [InlineData("""{"openapi":"4.0.0","paths":{}}""")]
    [InlineData("""{"swagger":"1.2","paths":{}}""")]
    [InlineData("""{"openapi":"2.0","paths":{}}""")]
    public void Unsupported_version_throws(string text) => Assert.Throws<ImportException>(() => Run(text, out _));
}
