using Yaapic.Core.Import;
using Yaapic.Core.Import.Insomnia;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests.Import.Insomnia;

public class InsomniaEnvironmentsTests
{
    private const string Workspace = "{\"_id\":\"wrk_1\",\"_type\":\"workspace\",\"name\":\"My API\"}";

    private static string Env(string id, string parent, string name, string data) =>
        "{\"_type\":\"environment\",\"_id\":\"" + id + "\",\"parentId\":\"" + parent + "\",\"name\":\"" + name + "\",\"data\":" + data + "}";

    private static ImportBundle Run(params string[] resources) =>
        new InsomniaImporter().Import(
            ImportInput.FromText("insomnia", "{\"__export_format\":4,\"resources\":[" + string.Join(',', resources) + "]}"),
            new ImportContext(SequentialIds.Create(), ImportOptions.Default));

    private static (string, string)[] Rows(IEnumerable<KeyValueEntry> entries) => [.. entries.Select(e => (e.Key, e.Value))];

    [Fact]
    public void Base_environment_becomes_collection_variables()
    {
        var bundle = Run(
            Workspace,
            Env("env_1", "wrk_1", "Base Environment",
                "{\"api\":{\"host\":\"x\"},\"n\":42,\"b\":true,\"z\":null,\"arr\":[1,\"a\"],\"t\":\"{{ _.api.host }}/v1\"}"));

        Assert.Equal(
            [("api.host", "x"), ("n", "42"), ("b", "true"), ("z", ""), ("arr", "[1,\"a\"]"), ("t", "{{api.host}}/v1")],
            Rows(bundle.Collection!.Folder.Variables));
        Assert.All(bundle.Collection.Folder.Variables, v => Assert.True(v.Enabled));
        Assert.Empty(bundle.Environments);
    }

    [Fact]
    public void Sub_environments_are_scoped_to_the_collection_in_order()
    {
        var bundle = Run(
            Workspace,
            Env("env_1", "wrk_1", "Base Environment", "{}"),
            Env("env_2", "env_1", "Dev", "{\"a\":\"1\"}"),
            Env("env_3", "env_1", "Prod", "{\"a\":\"2\"}"));

        Assert.Equal(["Dev", "Prod"], bundle.Environments.Select(e => e.Name));
        Assert.All(bundle.Environments, e => Assert.Equal(bundle.Collection!.Folder.Id, e.CollectionId));
        Assert.Equal([("a", "2")], Rows(bundle.Environments[1].Variables));
    }

    [Fact]
    public void Several_base_environments_merge_with_later_keys_replacing()
    {
        var bundle = Run(
            Workspace,
            Env("env_1", "wrk_1", "Base", "{\"a\":\"1\",\"b\":\"2\"}"),
            Env("env_2", "wrk_1", "Base 2", "{\"a\":\"3\",\"c\":\"4\"}"));

        Assert.Equal([("a", "3"), ("b", "2"), ("c", "4")], Rows(bundle.Collection!.Folder.Variables));
    }

    [Fact]
    public void Orphan_environment_is_attached_as_a_sub_environment()
    {
        var bundle = Run(Workspace, Env("env_9", "nowhere", "Lost", "{\"a\":\"1\"}"));

        var env = Assert.Single(bundle.Environments);
        Assert.Equal("Lost", env.Name);
        Assert.Equal(bundle.Collection!.Folder.Id, env.CollectionId);
        Assert.Empty(bundle.Collection.Folder.Variables);
    }

    [Fact]
    public void Empty_data_is_still_imported()
    {
        var bundle = Run(Workspace, Env("env_1", "wrk_1", "Base", "{}"), Env("env_2", "env_1", "Empty", "{}"));

        Assert.Empty(Assert.Single(bundle.Environments).Variables);
        Assert.Empty(bundle.Collection!.Folder.Variables);
    }

    [Fact]
    public void Objects_deeper_than_the_limit_are_written_as_json()
    {
        var bundle = Run(
            Workspace,
            Env("env_1", "wrk_1", "Base", "{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":1}}}}},\"x\":{\"y\":{\"z\":{\"w\":{\"v\":2}}}}}"));

        Assert.Equal([("a.b.c.d.e", "{\"f\":1}"), ("x.y.z.w.v", "2")], Rows(bundle.Collection!.Folder.Variables));
    }

    [Fact]
    public void Importer_end_to_end()
    {
        var bundle = Run(
            Workspace,
            "{\"_type\":\"request_group\",\"_id\":\"fld_1\",\"parentId\":\"wrk_1\",\"name\":\"Users\"}",
            "{\"_type\":\"request\",\"_id\":\"req_1\",\"parentId\":\"fld_1\",\"name\":\"List\",\"method\":\"GET\",\"url\":\"{{ _.host }}/users\"}",
            Env("env_1", "wrk_1", "Base Environment", "{\"host\":\"http://a\"}"),
            Env("env_2", "env_1", "Staging", "{\"host\":\"http://s\"}"));

        Assert.Equal(1, bundle.RequestCount);
        Assert.Equal(2, bundle.FolderCount);
        Assert.Equal([("host", "http://a")], Rows(bundle.Collection!.Folder.Variables));
        var staging = Assert.Single(bundle.Environments);
        Assert.Equal([("host", "http://s")], Rows(staging.Variables));
        Assert.Equal(bundle.Collection.Folder.Id, staging.CollectionId);
    }
}
