using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.OpenApi;

public class SwaggerNormalizerTests
{
    private static string Convert(string json, out ImportReport report)
    {
        report = new ImportReport();
        return DataNodeJson.Write(SwaggerNormalizer.ToOpenApi3(DataNodeJson.Parse(json), report), indented: false);
    }

    private static string Convert(string json) => Convert(json, out _);

    [Fact]
    public void Servers_come_from_host_base_path_and_schemes()
    {
        var result = Convert("""{"swagger":"2.0","info":{"title":"T"},"host":"api.test","basePath":"/v1/","schemes":["https","http"],"tags":[{"name":"a"}],"paths":{}}""", out var report);
        Assert.Equal("""{"openapi":"3.0.3","info":{"title":"T"},"servers":[{"url":"https://api.test/v1"},{"url":"http://api.test/v1"}],"tags":[{"name":"a"}],"paths":{}}""", result);
        Assert.Single(report.Entries);
        Assert.Equal(ImportSeverity.Info, report.Entries[0].Severity);

        Assert.Contains("""[{"url":"https://api.test"}]""", Convert("""{"swagger":"2.0","host":"api.test","paths":{}}"""));
        Assert.Contains("""[{"url":"/base"}]""", Convert("""{"swagger":"2.0","basePath":"/base","paths":{}}"""));
        Assert.DoesNotContain("servers", Convert("""{"swagger":"2.0","paths":{}}"""));
    }

    [Fact]
    public void Body_parameter_becomes_request_body_with_consumes()
    {
        var doc = """{"swagger":"2.0","consumes":["text/plain"],"paths":{"/a":{"post":{"summary":"s","consumes":["application/json","application/xml"],"responses":{"200":{}},"produces":["x/y"],"parameters":[{"in":"body","name":"b","required":true,"schema":{"$ref":"#/definitions/Pet"}}]}},"/b":{"put":{"parameters":[{"in":"body","name":"b","schema":{"type":"string"}}]}},"/c":{"put":{"parameters":[{"in":"body","name":"b","schema":{"type":"string"}}]}}},"definitions":{"Pet":{"type":"object","properties":{"o":{"$ref":"#/definitions/Pet"}}}}}""";
        Assert.Equal(
            """{"openapi":"3.0.3","paths":{"/a":{"post":{"summary":"s","requestBody":{"content":{"application/json":{"schema":{"$ref":"#/components/schemas/Pet"}},"application/xml":{"schema":{"$ref":"#/components/schemas/Pet"}}},"required":true}}},"/b":{"put":{"requestBody":{"content":{"text/plain":{"schema":{"type":"string"}}}}}},"/c":{"put":{"requestBody":{"content":{"text/plain":{"schema":{"type":"string"}}}}}}},"components":{"schemas":{"Pet":{"type":"object","properties":{"o":{"$ref":"#/components/schemas/Pet"}}}}}}""",
            Convert(doc));
        Assert.Contains("""{"application/json":{"schema":{"type":"string"}}}""", Convert("""{"swagger":"2.0","paths":{"/b":{"put":{"parameters":[{"in":"body","name":"b","schema":{"type":"string"}}]}}}}"""));
    }

    [Fact]
    public void Form_data_picks_the_media_type()
    {
        var urlencoded = Convert("""{"swagger":"2.0","paths":{"/f":{"post":{"parameters":[{"in":"formData","name":"a","type":"string","description":"d"},{"in":"formData","name":"n","type":"integer","format":"int32"}]}}}}""");
        Assert.Equal(
            """{"openapi":"3.0.3","paths":{"/f":{"post":{"requestBody":{"content":{"application/x-www-form-urlencoded":{"schema":{"type":"object","properties":{"a":{"type":"string","description":"d"},"n":{"type":"integer","format":"int32"}}}}}}}}}}""",
            urlencoded);

        var multipart = Convert("""{"swagger":"2.0","paths":{"/f":{"post":{"parameters":[{"in":"formData","name":"a","type":"string"},{"in":"formData","name":"file","type":"file"}]}}}}""");
        Assert.Equal(
            """{"openapi":"3.0.3","paths":{"/f":{"post":{"requestBody":{"content":{"multipart/form-data":{"schema":{"type":"object","properties":{"a":{"type":"string"},"file":{"type":"string","format":"binary"}}}}}}}}}}""",
            multipart);

        Assert.Contains("multipart/form-data", Convert("""{"swagger":"2.0","consumes":["multipart/form-data"],"paths":{"/f":{"post":{"parameters":[{"in":"formData","name":"a","type":"string"}]}}}}"""));
    }

    [Fact]
    public void Plain_parameters_move_under_schema_and_refs_are_inlined()
    {
        var doc = """{"swagger":"2.0","parameters":{"limit":{"name":"limit","in":"query","type":"integer","format":"int32","default":10}},"paths":{"/p/{id}":{"parameters":[{"name":"id","in":"path","required":true,"type":"string"},{"in":"body","name":"ignored","schema":{}}],"get":{"parameters":[{"$ref":"#/parameters/limit"},{"name":"X-T","in":"header","type":"array","items":{"type":"string"},"collectionFormat":"csv"},{"name":"s","in":"query","type":"string","enum":["a","b"]}]}}}}""";
        Assert.Equal(
            """{"openapi":"3.0.3","paths":{"/p/{id}":{"parameters":[{"name":"id","in":"path","required":true,"schema":{"type":"string"}}],"get":{"parameters":[{"name":"limit","in":"query","schema":{"type":"integer","format":"int32","default":10}},{"name":"X-T","in":"header","schema":{"type":"array","items":{"type":"string"}}},{"name":"s","in":"query","schema":{"type":"string","enum":["a","b"]}}]}}}}""",
            Convert(doc));
    }

    [Fact]
    public void Security_definitions_are_converted()
    {
        var doc = """{"swagger":"2.0","paths":{},"security":[{"k":[]}],"securityDefinitions":{"b":{"type":"basic"},"k":{"type":"apiKey","name":"X","in":"header"},"app":{"type":"oauth2","flow":"application","tokenUrl":"t","scopes":{"r":"d"}},"pw":{"type":"oauth2","flow":"password","tokenUrl":"t","scopes":{}},"code":{"type":"oauth2","flow":"accessCode","authorizationUrl":"a","tokenUrl":"t","scopes":{}},"imp":{"type":"oauth2","flow":"implicit","authorizationUrl":"a","scopes":{}}}}""";
        Assert.Equal(
            """{"openapi":"3.0.3","paths":{},"components":{"securitySchemes":{"b":{"type":"http","scheme":"basic"},"k":{"type":"apiKey","name":"X","in":"header"},"app":{"type":"oauth2","flows":{"clientCredentials":{"tokenUrl":"t","scopes":{"r":"d"}}}},"pw":{"type":"oauth2","flows":{"password":{"tokenUrl":"t","scopes":{}}}},"code":{"type":"oauth2","flows":{"authorizationCode":{"authorizationUrl":"a","tokenUrl":"t","scopes":{}}}},"imp":{"type":"oauth2","flows":{"implicit":{"authorizationUrl":"a","scopes":{}}}}}},"security":[{"k":[]}]}""",
            Convert(doc));
    }

    private sealed class NoYaml : IYamlTreeReader
    {
        public DataNode Parse(string yaml) => throw new NotSupportedException();
    }

    private static ImportFolder Import(string json)
    {
        var context = new ImportContext(SequentialIds.Create(), ImportOptions.Default);
        var importer = new OpenApiImporter(new StructuredTextReader(new NoYaml()));
        return Assert.IsType<ImportFolder>(importer.Import(ImportInput.FromText("api.json", json), context).Collection);
    }

    private static string Summary(ImportFolder folder) => string.Join(
        "|",
        folder.Children.OfType<ImportRequest>().Select(r =>
            $"{r.Request.Name} {r.Request.Method} {r.Request.Url} {r.Request.Body.Kind} [{string.Join(",", r.Request.QueryParams.Select(q => q.Key))}]"));

    [Fact]
    public void Swagger_and_openapi_equivalents_import_to_the_same_requests()
    {
        var swagger = """{"swagger":"2.0","info":{"title":"T"},"host":"h.test","basePath":"/v1","schemes":["https"],"paths":{"/pets":{"get":{"summary":"List","parameters":[{"name":"limit","in":"query","type":"integer"}]},"post":{"summary":"Add","consumes":["application/json"],"parameters":[{"in":"body","name":"b","schema":{"type":"object","properties":{"n":{"type":"string"}}}}]}}}}""";
        var openApi = """{"openapi":"3.0.3","info":{"title":"T"},"servers":[{"url":"https://h.test/v1"}],"paths":{"/pets":{"get":{"summary":"List","parameters":[{"name":"limit","in":"query","schema":{"type":"integer"}}]},"post":{"summary":"Add","requestBody":{"content":{"application/json":{"schema":{"type":"object","properties":{"n":{"type":"string"}}}}}}}}}}""";

        var fromSwagger = Summary(Import(swagger));
        Assert.Equal(Summary(Import(openApi)), fromSwagger);
        Assert.Contains("Add Post", fromSwagger);
        Assert.Contains("[limit]", fromSwagger);
    }
}
