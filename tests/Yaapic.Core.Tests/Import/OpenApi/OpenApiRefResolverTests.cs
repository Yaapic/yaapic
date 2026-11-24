using Yaapic.Core.Import;
using Yaapic.Core.Import.OpenApi;

namespace Yaapic.Core.Tests.Import.OpenApi;

public class OpenApiRefResolverTests
{
    private const string Doc = """
        {
          "components": {
            "schemas": {
              "Pet": {"type": "object"},
              "Alias": {"$ref": "#/components/schemas/Pet"},
              "Alias2": {"$ref": "#/components/schemas/Alias"},
              "A": {"$ref": "#/components/schemas/B"},
              "B": {"$ref": "#/components/schemas/A"},
              "a/b": {"type": "slash"},
              "t~x": {"type": "tilde"},
              "with space": {"type": "space"}
            }
          },
          "list": [{"v": 1}, {"v": 2}]
        }
        """;

    private readonly ImportReport _report = new();
    private readonly DataNode _root = DataNodeJson.Parse(Doc);
    private readonly OpenApiRefResolver _resolver;

    public OpenApiRefResolverTests() => _resolver = new OpenApiRefResolver(_root, _report);

    private static DataNode Ref(string target) => DataNode.Object([KeyValuePair.Create("$ref", DataNode.String(target))]);

    private static string Type(DataNode node) => node.GetString("type") ?? "?";

    [Fact]
    public void A_non_ref_node_is_returned_as_is()
    {
        var node = _root["components"]!;
        Assert.Same(node, _resolver.Resolve(node, "p"));
        Assert.Empty(_report.Entries);
    }

    [Fact]
    public void A_simple_ref_returns_the_target_node_itself()
    {
        var resolved = _resolver.Resolve(Ref("#/components/schemas/Pet"), "p");
        Assert.Same(_root["components"]!["schemas"]!["Pet"], resolved);
    }

    [Fact]
    public void A_chain_is_followed() =>
        Assert.Equal("object", Type(_resolver.Resolve(Ref("#/components/schemas/Alias2"), "p")));

    [Theory]
    [InlineData("#/components/schemas/a~1b", "slash")]
    [InlineData("#/components/schemas/t~0x", "tilde")]
    [InlineData("#/components/schemas/with%20space", "space")]
    public void Pointer_escapes_are_decoded(string reference, string expected) =>
        Assert.Equal(expected, Type(_resolver.Resolve(Ref(reference), "p")));

    [Fact]
    public void Array_indexes_are_followed() =>
        Assert.Equal("2", _resolver.Resolve(Ref("#/list/1"), "p").GetString("v"));

    [Theory]
    [InlineData("other.yaml#/Pet")]
    [InlineData("https://a.test/schema.json")]
    public void External_refs_warn_and_return_null(string reference)
    {
        Assert.True(_resolver.Resolve(Ref(reference), "p").IsNull);
        var entry = Assert.Single(_report.Entries);
        Assert.Equal(ImportSeverity.Warning, entry.Severity);
        Assert.Contains(reference, entry.Message);
    }

    [Fact]
    public void A_missing_target_warns_and_returns_null()
    {
        Assert.True(_resolver.Resolve(Ref("#/components/schemas/Nope"), "p").IsNull);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(_report.Entries).Severity);
    }

    [Fact]
    public void A_cycle_warns_and_returns_null()
    {
        Assert.True(_resolver.Resolve(Ref("#/components/schemas/A"), "p").IsNull);
        Assert.Equal(ImportSeverity.Warning, Assert.Single(_report.Entries).Severity);
    }

    [Fact]
    public void A_chain_longer_than_twenty_warns()
    {
        var members = Enumerable.Range(0, 25).Select(i =>
            KeyValuePair.Create($"r{i}", i == 24 ? DataNodeJson.Parse("""{"type":"end"}""") : Ref($"#/r{i + 1}")));
        var root = DataNode.Object([.. members]);
        var resolver = new OpenApiRefResolver(root, _report);
        Assert.True(resolver.Resolve(root["r0"]!, "p").IsNull);
        Assert.Single(_report.Entries);
    }

    [Fact]
    public void Sibling_keys_of_a_ref_are_ignored()
    {
        var node = DataNode.Object([
            KeyValuePair.Create("$ref", DataNode.String("#/components/schemas/Pet")),
            KeyValuePair.Create("description", DataNode.String("ignored"))]);
        Assert.Null(_resolver.Resolve(node, "p").GetString("description"));
    }

    [Fact]
    public void Get_resolves_a_member_and_returns_null_when_absent()
    {
        var holder = DataNode.Object([KeyValuePair.Create("schema", Ref("#/components/schemas/Pet"))]);
        Assert.Equal("object", Type(_resolver.Get(holder, "schema", "p")!));
        Assert.Null(_resolver.Get(holder, "none", "p"));
    }
}
