using Yaapic.Core.Mocks;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Mocks;

namespace Yaapic.Core.Tests.Mocks;

public class MockGeneratorTests
{
    [Theory]
    [InlineData("https://api.test/users/{{id}}?x=1", "/users/:id")]
    [InlineData("{{baseUrl}}/users", "/users")]
    [InlineData("{{baseUrl}}", "/")]
    [InlineData("api.test/v1/items", "/v1/items")]
    [InlineData("/plain", "/plain")]
    [InlineData("http://host:8080", "/")]
    [InlineData("{{baseUrl}}/files/{{name}}.json", "/files/{{name}}.json")]
    [InlineData("", "/")]
    [InlineData("/a#frag", "/a")]
    [InlineData("/a/{{q?x}}/b?y=1", "/a/:q?x/b")]
    public void PathOf_extracts_the_route_path(string url, string expected) =>
        Assert.Equal(expected, MockGenerator.PathOf(url));

    private static CollectionNode Req(string name, HttpMethodKind method, string url) =>
        CollectionNode.ForRequest(HttpRequestDefinition.Create(name) with { Method = method, Url = url });

    [Fact]
    public void FromFolder_lists_routes_in_run_order_skips_duplicates_and_validates()
    {
        var folder = CollectionNode.ForFolder(FolderDefinition.Create("Shop"),
        [
            CollectionNode.ForFolder(FolderDefinition.Create("Admin"), [Req("Users", HttpMethodKind.Get, "{{baseUrl}}/users/{{id}}")]),
            Req("List", HttpMethodKind.Get, "{{baseUrl}}/items"),
            Req("List again", HttpMethodKind.Get, "{{baseUrl}}/items?page=2"),
            Req("Create", HttpMethodKind.Post, "{{baseUrl}}/items"),
        ]);

        var result = MockGenerator.FromFolder(folder);

        Assert.Equal(1, result.Skipped);
        Assert.Equal(["Users", "Create", "List"], result.Server.Routes.Select(r => r.Name));
        Assert.Equal(["/users/:id", "/items", "/items"], result.Server.Routes.Select(r => r.Path));
        Assert.Equal(["GET", "POST", "GET"], result.Server.Routes.Select(r => r.Method));
        Assert.Equal(3000, result.Server.Port);
        Assert.True(result.Server.Cors);
        Assert.Empty(MockServerValidator.Validate(result.Server));
    }
}
