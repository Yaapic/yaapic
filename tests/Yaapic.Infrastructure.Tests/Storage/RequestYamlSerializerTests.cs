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
        HttpMethodKind.Put,
        "{{baseUrl}}/users",
        [new("page", "1"), new("debug", "{{flag}}", false)],
        [new("Accept", "application/json")],
        RequestBody.Raw(RawContentType.Json, "{\n  \"name\": \"Zoë\",\n  \"id\": \"{{id}}\"\n}") with
        {
            Fields = [new("a", "{{v}}", false), new("avatar", "img.png", true, FormFieldKind.File)],
            FilePath = "blob.bin",
        },
        new(AuthKind.ApiKey, "u", "p", "t", "X-Key", "{{secret}}", ApiKeyLocation.Query),
        new RequestSettings(false, 15));

    private static HttpRequestDefinition With(RequestBody body, RequestAuth? auth = null) =>
        Sample() with { Body = body, Auth = auth ?? RequestAuth.None };

    private static string Golden(string name = "request-v1.request.yaml") =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Storage", "golden", name));

    public static TheoryData<RequestBody> Bodies() => new()
    {
        RequestBody.UrlEncoded([new("a", "1"), new("b", "{{v}}", false)]),
        RequestBody.FormData([new("t", "x"), new("avatar", @"C:\img\é.png", true, FormFieldKind.File)]),
        RequestBody.Binary(@"C:\datalob.bin"),
        RequestBody.Raw(RawContentType.Html, "<p>é</p>"),
        RequestBody.Raw(RawContentType.JavaScript, "x()"),
    };

    public static TheoryData<RequestAuth> Auths() => new()
    {
        RequestAuth.None,
        new RequestAuth(AuthKind.Basic, "u", "p", "", "", "", ApiKeyLocation.Header),
        new RequestAuth(AuthKind.Bearer, "", "", "{{token}}", "", "", ApiKeyLocation.Header),
        new RequestAuth(AuthKind.ApiKey, "", "", "", "k", "v", ApiKeyLocation.Query),
    };

    [Theory]
    [MemberData(nameof(Bodies))]
    public void RoundTripKeepsBodyKinds(RequestBody body)
    {
        var request = With(body);
        Assert.True(request.IsEquivalentTo(RequestYamlSerializer.Deserialize(RequestYamlSerializer.Serialize(request))));
    }

    [Theory]
    [MemberData(nameof(Auths))]
    public void RoundTripKeepsAuth(RequestAuth auth)
    {
        var request = With(RequestBody.None, auth);
        Assert.True(request.IsEquivalentTo(RequestYamlSerializer.Deserialize(RequestYamlSerializer.Serialize(request))));
    }

    [Fact]
    public void LegacyM1FileReadsWithDefaults()
    {
        var request = RequestYamlSerializer.Deserialize(Golden("request-m1-legacy.request.yaml"));
        Assert.Equal(HttpMethodKind.Post, request.Method);
        Assert.Equal(RequestAuth.None, request.Auth);
        Assert.Equal(RequestSettings.Default, request.Settings);
        Assert.Empty(request.Body.Fields);
        Assert.Equal("", request.Body.FilePath);
    }

    [Fact]
    public void MissingFieldEnabledAndKindTakeDefaults()
    {
        var yaml = $"version: 1\nid: {IdText}\nmethod: GET\nbody:\n  type: urlencoded\n  fields:\n    - key: a\n";
        Assert.Equal(new FormField("a", ""), Assert.Single(RequestYamlSerializer.Deserialize(yaml).Body.Fields));
    }

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
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: TRACE\n")]
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\nauth:\n  type: digest\n")]
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\nauth:\n  type: apikey\n  in: cookie\n")]
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\nbody:\n  type: urlencoded\n  fields:\n    - key: a\n      kind: blob\n")]
    [InlineData("version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\nsettings:\n  timeoutSeconds: -1\n")]
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
