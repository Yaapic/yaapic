using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.OpenApi;

public class OpenApiOperationsTests
{
    private static readonly OpenApiImporter Importer = new(new StructuredTextReader(new NoYaml()));

    private sealed class NoYaml : IYamlTreeReader
    {
        public DataNode Parse(string yaml) => throw new NotSupportedException();
    }

    private static ImportBundle Run(string json, out ImportContext context)
    {
        context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        return Importer.Import(ImportInput.FromText("api.json", json), context);
    }

    private static string Doc(string paths, string extra = "") =>
        "{\"openapi\":\"3.0.3\",\"info\":{\"title\":\"T\",\"description\":\"D\"}," + extra.Replace((char)39, (char)34) + "\"paths\":" + paths + "}";

    private static ImportFolder Root(ImportBundle bundle) => Assert.IsType<ImportFolder>(bundle.Collection);

    private static HttpRequestDefinition Req(ImportItem item) => Assert.IsType<ImportRequest>(item).Request;

    private static string Messages(ImportContext context) => string.Join("\n", context.Report.Entries.Select(e => e.Message));

    [Fact]
    public void Tags_make_folders_and_untagged_operations_stay_at_the_root()
    {
        var bundle = Run(Doc("""
            {"/pets":{"get":{"tags":["pets"],"summary":"List"},"post":{"tags":["pets"],"operationId":"addPet"}},
             "/users":{"get":{"tags":["users"]}},
             "/ping":{"head":{}}}
            """, "'tags':[{'name':'pets','description':'Pet ops'}],"), out _);
        var root = Root(bundle);
        Assert.Equal(["baseUrl"], root.Folder.Variables.Select(v => v.Key));
        Assert.Equal(3, root.Children.Count);
        var pets = Assert.IsType<ImportFolder>(root.Children[0]);
        Assert.Equal("pets", pets.Folder.Name);
        Assert.Equal("Pet ops", pets.Folder.Description);
        Assert.Equal(["List", "addPet"], pets.Children.Select(c => Req(c).Name));
        var users = Assert.IsType<ImportFolder>(root.Children[1]);
        Assert.Equal("", users.Folder.Description);
        Assert.Equal("GET /users", Req(Assert.Single(users.Children)).Name);
        Assert.Equal(HttpMethodKind.Head, Req(root.Children[2]).Method);
        Assert.Equal(3, bundle.FolderCount);
        Assert.Equal(4, bundle.RequestCount);
    }

    [Fact]
    public void Url_uses_base_url_and_path_variables()
    {
        var bundle = Run(Doc("""
            {"/pets/{id}/toys/{toy}":{"get":{"parameters":[
              {"name":"id","in":"path","required":true,"schema":{"type":"string","example":"7"}},
              {"name":"toy","in":"path","required":true}]}},
             "/pets/{id}":{"delete":{"parameters":[{"name":"id","in":"path","example":"9"}]}}}
            """), out _);
        var root = Root(bundle);
        Assert.Equal("{{baseUrl}}/pets/{{id}}/toys/{{toy}}", Req(root.Children[0]).Url);
        Assert.Equal([("baseUrl", ""), ("id", "7"), ("toy", "")], root.Folder.Variables.Select(v => (v.Key, v.Value)));
    }

    [Fact]
    public void Single_server_becomes_the_base_url_without_trailing_slash()
    {
        var bundle = Run(Doc("{}", "'servers':[{'url':'https://api.example.com/v1/'}],"), out var context);
        Assert.Equal("https://api.example.com/v1", Root(bundle).Folder.Variables[0].Value);
        Assert.Empty(bundle.Environments);
        Assert.Empty(context.Report.Entries);
        Assert.Equal(1, bundle.FolderCount);
        Assert.Equal(0, bundle.RequestCount);
    }

    [Fact]
    public void Further_servers_become_scoped_environments()
    {
        var bundle = Run(Doc("{}", "'servers':[{'url':'https://a'},{'url':'https://b','description':'Staging'},{'url':'https://c'}],"), out _);
        var root = Root(bundle);
        Assert.Equal("https://a", root.Folder.Variables[0].Value);
        Assert.Equal(["Staging", "https://c"], bundle.Environments.Select(e => e.Name));
        Assert.All(bundle.Environments, e => Assert.Equal(root.Folder.Id, e.CollectionId));
        Assert.Equal("https://b", Assert.Single(bundle.Environments[0].Variables).Value);
    }

    [Fact]
    public void Server_variables_use_their_default_or_warn()
    {
        var bundle = Run(Doc("{}", "'servers':[{'url':'https://{host}.x/{ver}','variables':{'host':{'default':'api'},'ver':{'enum':['v1']}}}],"), out var context);
        Assert.Equal("https://api.x/{ver}", Root(bundle).Folder.Variables[0].Value);
        var entry = Assert.Single(context.Report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Contains("ver", entry.Message);
    }

    [Theory]
    [InlineData("""{"servers":[{"url":"/api/v1"}],"paths":{}}""", "/api/v1")]
    [InlineData("""{"paths":{}}""", "")]
    public void Relative_or_missing_server_gets_an_info(string body, string baseUrl)
    {
        var bundle = Run("{\"openapi\":\"3.0.0\"," + body[1..], out var context);
        Assert.Equal(baseUrl, Root(bundle).Folder.Variables[0].Value);
        Assert.Equal(ImportSeverity.Info, Assert.Single(context.Report.Entries).Severity);
    }

    [Fact]
    public void Query_and_header_parameters_are_enabled_only_when_required()
    {
        var bundle = Run(Doc("""
            {"/a":{"get":{"parameters":[
              {"name":"q1","in":"query","required":true,"example":"x"},
              {"name":"q2","in":"query"},
              {"name":"h1","in":"header","required":true},
              {"name":"Accept","in":"header"},
              {"name":"content-type","in":"header"},
              {"name":"Authorization","in":"header"}]}}}
            """), out _);
        var request = Req(Root(bundle).Children[0]);
        Assert.Equal([new KeyValueEntry("q1", "x"), new KeyValueEntry("q2", "", false)], request.QueryParams);
        Assert.Equal([new KeyValueEntry("h1", "", true)], request.Headers);
    }

    [Fact]
    public void Sample_value_sources_follow_the_stated_order()
    {
        var bundle = Run(Doc("""
            {"/a":{"get":{"parameters":[
              {"name":"p1","in":"query","example":"E","examples":{"k":{"value":"X"}},"schema":{"example":"S"}},
              {"name":"p2","in":"query","examples":{"k":{"value":"X"}},"schema":{"example":"S"}},
              {"name":"p3","in":"query","schema":{"example":"S","default":"D"}},
              {"name":"p4","in":"query","schema":{"default":"D","enum":["N"]}},
              {"name":"p5","in":"query","schema":{"enum":["N","M"]}},
              {"name":"p6","in":"query","schema":{"type":"string"}},
              {"name":"p7","in":"query","example":5},
              {"name":"p8","in":"query","example":["a","b"]},
              {"name":"p9","in":"query","style":"deepObject","example":{"a":1}},
              {"name":"p10","in":"query","style":"pipeDelimited","example":["a","b"]}]}}}
            """), out _);
        Assert.Equal(["E", "X", "S", "D", "N", "", "5", "a,b", "{\"a\":1}", "[\"a\",\"b\"]"],
            Req(Root(bundle).Children[0]).QueryParams.Select(p => p.Value));
    }

    [Fact]
    public void Operation_parameters_override_path_item_parameters()
    {
        var bundle = Run(Doc("""
            {"/a":{"parameters":[{"name":"q","in":"query","example":"item"},{"name":"r","in":"query","example":"keep"}],
                   "get":{"parameters":[{"name":"q","in":"query","required":true,"example":"op"}]}}}
            """), out _);
        Assert.Equal([new KeyValueEntry("q", "op"), new KeyValueEntry("r", "keep", false)], Req(Root(bundle).Children[0]).QueryParams);
    }

    [Fact]
    public void Parameters_can_be_references_and_cookies_are_counted()
    {
        var bundle = Run(Doc("""
            {"/a":{"get":{"parameters":[{"$ref":"#/components/parameters/Limit"},
              {"name":"c1","in":"cookie"},{"name":"c2","in":"cookie"}]}}}
            """, "'components':{'parameters':{'Limit':{'name':'limit','in':'query','required':true,'schema':{'default':10}}}},"), out var context);
        Assert.Equal([new KeyValueEntry("limit", "10")], Req(Root(bundle).Children[0]).QueryParams);
        Assert.Contains("2 cookie", Messages(context));
    }

    [Fact]
    public void Trace_is_skipped_and_deprecated_and_descriptions_are_reported()
    {
        var bundle = Run(Doc("""
            {"/a":{"trace":{},"x-note":1,"get":{"deprecated":true,"description":"long"},"post":{"description":"more"}}}
            """), out var context);
        Assert.Equal(2, Root(bundle).Children.Count);
        Assert.Contains(context.Report.Entries, e => e.Severity == ImportSeverity.Skipped && e.Message.Contains("TRACE"));
        Assert.Equal(1, context.Report.Count(ImportSeverity.Skipped));
        Assert.Contains("GET /a", Messages(context));
        Assert.Contains("2", Messages(context));
    }

    [Fact]
    public void Local_servers_are_ignored_with_one_info()
    {
        var bundle = Run(Doc("""
            {"/a":{"servers":[{"url":"https://x"}],"get":{"servers":[{"url":"https://y"}]},"put":{"servers":[]}}}
            """, "'servers':[{'url':'https://g'}],"), out var context);
        Assert.Equal(2, bundle.RequestCount);
        Assert.Equal(1, context.Report.Entries.Count(e => e.Message.Contains("Servers declared")));
    }

    [Fact]
    public void Path_item_reference_is_resolved()
    {
        var bundle = Run(Doc("""{"/a":{"$ref":"#/components/x/Shared"}}""",
            "'components':{'x':{'Shared':{'get':{'summary':'Shared op'}}}},"), out _);
        Assert.Equal("Shared op", Req(Root(bundle).Children[0]).Name);
    }

    [Fact]
    public void Empty_paths_give_only_the_collection_folder()
    {
        var bundle = Run(Doc("{}", "'servers':[{'url':'https://a'}],"), out _);
        Assert.Equal(1, bundle.FolderCount);
        Assert.Equal(0, bundle.RequestCount);
        Assert.Equal(new FolderDefinition(Root(bundle).Folder.Id, "T", "D", [new KeyValueEntry("baseUrl", "https://a")]).Variables,
            Root(bundle).Folder.Variables);
    }
}
