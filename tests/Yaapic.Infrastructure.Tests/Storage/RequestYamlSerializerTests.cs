using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class RequestYamlSerializerTests
{
    private const string IdText = "6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8";
    private static readonly Guid Id = Guid.Parse(IdText);

    private static HttpRequestDefinition Sample() => new(
        Id,
        "Get users – café ☕",
        HttpMethodKind.Post,
        "{{baseUrl}}/users",
        [new("page", "1"), new("debug", "{{flag}}", false)],
        [new("Accept", "application/json")],
        RequestBody.Raw(RawContentType.Json, "{\n  \"name\": \"Zoë\",\n  \"id\": \"{{id}}\"\n}"));

    private static string Golden() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Storage", "golden", "request-v1.request.yaml"));

    [Fact]
    public void RoundTripKeepsEquivalence()
    {
        var request = Sample();
        var back = RequestYamlSerializer.Deserialize(RequestYamlSerializer.Serialize(request));
        Assert.True(request.IsEquivalentTo(back));
    }

    [Fact]
    public void RoundTripKeepsEmptyRequest()
    {
        var request = HttpRequestDefinition.Create("Empty");
        var back = RequestYamlSerializer.Deserialize(RequestYamlSerializer.Serialize(request));
        Assert.True(request.IsEquivalentTo(back));
    }

    [Fact]
    public void SerializeMatchesGoldenFile() => Assert.Equal(Golden(), RequestYamlSerializer.Serialize(Sample()));

    [Fact]
    public void OutputUsesLfAndSingleTrailingNewline()
    {
        var yaml = RequestYamlSerializer.Serialize(Sample());
        Assert.DoesNotContain('\r', yaml);
        Assert.EndsWith("\n", yaml);
        Assert.False(yaml.EndsWith("\n\n", StringComparison.Ordinal));
    }

    [Fact]
    public void GoldenFileDeserializes() =>
        Assert.True(Sample().IsEquivalentTo(RequestYamlSerializer.Deserialize(Golden())));

    [Fact]
    public void UnknownKeysAreIgnored()
    {
        var yaml = $"version: 1\nid: {IdText}\nname: X\nmethod: GET\nfuture: {{a: 1}}\nother: 3\n";
        Assert.Equal("X", RequestYamlSerializer.Deserialize(yaml).Name);
    }

    [Fact]
    public void MissingOptionalKeysTakeDefaults()
    {
        var yaml = $"version: 1\nid: {IdText}\nmethod: GET\nparams:\n  - key: a\n    value: b\n";
        var request = RequestYamlSerializer.Deserialize(yaml);
        Assert.Equal("Untitled", request.Name);
        Assert.Equal(new KeyValueEntry("a", "b", true), Assert.Single(request.QueryParams));
        Assert.Empty(request.Headers);
        Assert.Equal(RequestBody.None, request.Body);
    }

    [Theory]
    [InlineData("version: 2\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\n")]
    [InlineData("name: [unclosed\n")]
    [InlineData("version: 1\nmethod: GET\n")]
    [InlineData("version: 1\nid: nope\nmethod: GET\n")]
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: PUT\n")]
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\nbody:\n  type: form\n")]
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\nbody:\n  type: raw\n  contentType: yaml\n")]
    public void InvalidFilesThrowStorageException(string yaml) =>
        Assert.Throws<StorageException>(() => RequestYamlSerializer.Deserialize(yaml));

    [Fact]
    public void Version2MessageNamesTheVersion()
    {
        var ex = Assert.Throws<StorageException>(() =>
            RequestYamlSerializer.Deserialize($"version: 2\nid: {IdText}\nmethod: GET\n"));
        Assert.Equal("Unsupported request file version: 2", ex.Message);
    }
}
