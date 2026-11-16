using System.Net;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Http;

namespace Yaapic.Infrastructure.Tests;

internal sealed class FakeHttpVersionSupport(bool supported) : IHttpVersionSupport
{
    public bool IsHttp3Supported { get; } = supported;
}

public class HttpVersionMappingTests
{
    [Theory]
    [InlineData(HttpVersionPreference.Default, false, 1, 1, HttpVersionPolicy.RequestVersionOrHigher)]
    [InlineData(HttpVersionPreference.Http1, true, 1, 1, HttpVersionPolicy.RequestVersionExact)]
    [InlineData(HttpVersionPreference.Http2, false, 2, 0, HttpVersionPolicy.RequestVersionOrLower)]
    [InlineData(HttpVersionPreference.Http3, true, 3, 0, HttpVersionPolicy.RequestVersionOrLower)]
    [InlineData(HttpVersionPreference.Http3, false, 2, 0, HttpVersionPolicy.RequestVersionOrLower)]
    public void Apply_maps_preference(HttpVersionPreference pref, bool http3, int major, int minor, HttpVersionPolicy policy)
    {
        using var message = new HttpRequestMessage();
        HttpVersionMapping.Apply(message, pref, http3);
        Assert.Equal(new Version(major, minor), message.Version);
        Assert.Equal(policy, message.VersionPolicy);
    }

    [Theory]
    [InlineData(1, 1, "HTTP/1.1")]
    [InlineData(2, 0, "HTTP/2")]
    [InlineData(3, 0, "HTTP/3")]
    public void Protocol_text(int major, int minor, string expected) =>
        Assert.Equal(expected, HttpVersionMapping.ProtocolText(new Version(major, minor)));

    [Theory]
    [InlineData(HttpVersionPreference.Default)]
    [InlineData(HttpVersionPreference.Http1)]
    public async Task Plain_http_server_reports_http11(HttpVersionPreference pref)
    {
        using var listener = new HttpListener();
        var port = Random.Shared.Next(20000, 60000);
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        var serve = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            ctx.Response.Close();
        }, TestContext.Current.CancellationToken);
        using var transport = new HttpClientTransport(
            DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(10),
            support: new FakeHttpVersionSupport(false));

        var result = await transport.SendAsync(
            new ResolvedRequest(HttpMethodKind.Get, $"http://127.0.0.1:{port}/", [], null, null, [], HttpVersion: pref),
            TestContext.Current.CancellationToken);
        await serve;

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("HTTP/1.1", result.Response!.Protocol);
    }
}
