using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class SecretFlagYamlTests
{
    private const string IdText = "6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8";

    private static readonly KeyValueEntry[] Mixed =
    [
        new("baseUrl", "https://example.com"),
        new("token", "", true, true),
        new("off", "x", false),
        new("old", "", false, true),
    ];

    [Fact]
    public void EnvironmentRoundTripsSecretFlags()
    {
        var env = new EnvironmentDefinition(Guid.Parse(IdText), "E", Mixed);
        var back = EnvironmentYamlSerializer.Deserialize(EnvironmentYamlSerializer.Serialize(env));
        Assert.Equal(Mixed, back.Variables);
    }

    [Fact]
    public void FolderRoundTripsSecretFlags()
    {
        var folder = new FolderDefinition(Guid.Parse(IdText), "F", "", Mixed);
        var back = FolderYamlSerializer.Deserialize(FolderYamlSerializer.Serialize(folder));
        Assert.Equal(Mixed, back.Variables);
    }

    [Fact]
    public void GlobalsRoundTripsSecretFlags() =>
        Assert.Equal(Mixed, GlobalsYamlSerializer.Deserialize(GlobalsYamlSerializer.Serialize(Mixed)));

    [Fact]
    public void SecretIsWrittenAfterEnabledAndOnlyForSecretRows() =>
        Assert.Equal(
            "version: 1\nvariables:\n- key: a\n  value: b\n  enabled: true\n- key: t\n  value: \"\"\n  enabled: true\n  secret: true\n",
            GlobalsYamlSerializer.Serialize([new("a", "b"), new("t", "", true, true)]));

    [Fact]
    public void NonSecretOutputIsUnchanged() =>
        Assert.Equal(
            "version: 1\nvariables:\n- key: a\n  value: b\n  enabled: false\n",
            GlobalsYamlSerializer.Serialize([new("a", "b", false)]));

    [Fact]
    public void SecretIsIgnoredAndNeverWrittenForParamsAndHeaders()
    {
        var request = RequestYamlSerializer.Deserialize(
            $"version: 1\nid: {IdText}\nmethod: GET\nparams:\n- key: p\n  value: v\n  secret: true\nheaders:\n- key: h\n  value: v\n  secret: true\n");
        Assert.False(Assert.Single(request.QueryParams).IsSecret);
        Assert.False(Assert.Single(request.Headers).IsSecret);

        var forced = request with { QueryParams = [new("p", "v", true, true)], Headers = [new("h", "v", true, true)] };
        Assert.DoesNotContain("secret", RequestYamlSerializer.Serialize(forced));
    }

    [Fact]
    public void MilestoneTwoFilesLoadAsNotSecret()
    {
        Assert.All(
            GlobalsYamlSerializer.Deserialize("version: 1\nvariables:\n- key: a\n  value: b\n  enabled: true\n- key: c\n  value: d\n  secret: false\n"),
            v => Assert.False(v.IsSecret));
    }

    [Fact]
    public void EqualityIncludesTheFlag()
    {
        Assert.Equal(new KeyValueEntry("a", "b", true, true), new KeyValueEntry("a", "b", true, true));
        Assert.NotEqual(new KeyValueEntry("a", "b"), new KeyValueEntry("a", "b", true, true));
    }
}
