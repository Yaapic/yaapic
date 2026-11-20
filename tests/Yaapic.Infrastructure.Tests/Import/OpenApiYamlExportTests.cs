using System.Text;
using Yaapic.Core.Export;
using Yaapic.Core.Export.OpenApi;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Import;

namespace Yaapic.Infrastructure.Tests.Import;

public sealed class OpenApiYamlExportTests
{
    [Fact]
    public void YamlAndJsonExportersProduceTheSameDocument()
    {
        var request = new HttpRequestDefinition(
            Guid.NewGuid(), "Create: user", HttpMethodKind.Post, "https://x.test/users/:id?a=1", [new("page", "2")], [],
            RequestBody.Raw(RawContentType.Json, "{\"n\":1,\"list\":[true,null],\"s\":\"x\"}"),
            RequestAuth.None with { Kind = AuthKind.ApiKey, Key = "X-Key" }, RequestSettings.Default);
        var root = CollectionNode.ForFolder(
            new FolderDefinition(Guid.NewGuid(), "My API", "A description", []), [CollectionNode.ForRequest(request)]);
        var source = new ExportSource(root, [], [], root.Name);

        var json = Encoding.UTF8.GetString(new OpenApiJsonExporter().Export(source).Files.Single().Content);
        var yaml = Encoding.UTF8.GetString(new OpenApiYamlExporter().Export(source).Files.Single().Content);

        Assert.Equal(
            DataNodeJson.Write(DataNodeJson.Parse(json), indented: false),
            DataNodeJson.Write(new VYamlTreeReader().Parse(yaml), indented: false));
    }
}
