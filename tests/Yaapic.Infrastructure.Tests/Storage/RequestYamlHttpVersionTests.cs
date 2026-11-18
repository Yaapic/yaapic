using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Tests.Storage;

public sealed class RequestYamlHttpVersionTests
{
    private const string Header = "version: 1\nid: 6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8\nmethod: GET\n";

    public static TheoryData<HttpVersionPreference> Versions() => new(Enum.GetValues<HttpVersionPreference>());

    [Theory]
    [MemberData(nameof(Versions))]
    public void RoundTripKeepsEveryValue(HttpVersionPreference version)
    {
        var request = HttpRequestDefinition.Create("r") with { Settings = new RequestSettings(false, 7, version) };
        var back = RequestYamlSerializer.Deserialize(RequestYamlSerializer.Serialize(request));
        Assert.Equal(version, back.Settings.HttpVersion);
        Assert.Equal(request.Settings, back.Settings);
    }

    [Fact]
    public void DefaultIsNeverWritten() =>
        Assert.DoesNotContain("httpVersion", RequestYamlSerializer.Serialize(HttpRequestDefinition.Create("r")));

    [Fact]
    public void DefaultSettingsBlockIsUnchanged() =>
        Assert.Contains("settings:\n  followRedirects: true\n  timeoutSeconds: 0\n", RequestYamlSerializer.Serialize(HttpRequestDefinition.Create("r")));

    [Fact]
    public void WrittenAsLowerCaseKey() =>
        Assert.Contains("  httpVersion: http2\n", RequestYamlSerializer.Serialize(
            HttpRequestDefinition.Create("r") with { Settings = new RequestSettings(true, 0, HttpVersionPreference.Http2) }));

    [Theory]
    [InlineData("")]
    [InlineData("settings:\n  followRedirects: true\n")]
    [InlineData("settings:\n  httpVersion: default\n")]
    [InlineData("settings:\n  httpVersion: spdy\n")]
    public void MissingOrUnknownReadsAsDefault(string settings) =>
        Assert.Equal(HttpVersionPreference.Default, RequestYamlSerializer.Deserialize(Header + settings).Settings.HttpVersion);
}
