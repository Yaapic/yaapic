using System.Text.Json;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Http;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

public sealed class HttpClientTransportLoopbackTests : IClassFixture<LoopbackServer>, IDisposable
{
    private readonly LoopbackServer _server;
    private readonly HttpClientTransport _transport = new(
        DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(10));
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-transport-" + Guid.NewGuid().ToString("N"));

    public HttpClientTransportLoopbackTests(LoopbackServer server)
    {
        _server = server;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private ResolvedRequest Req(string path, HttpMethodKind method = HttpMethodKind.Get, string? body = null,
        string? contentType = null, IReadOnlyList<ResolvedPart>? parts = null, string? file = null,
        bool follow = true, int timeout = 0) =>
        new(method, _server.BaseUrl + path, [], body, contentType, [], parts, file, follow, timeout);

    private static JsonElement Echo(SendResult result) => JsonDocument.Parse(result.Response!.Body).RootElement;

    [Theory]
    [InlineData(HttpMethodKind.Get, "GET")]
    [InlineData(HttpMethodKind.Post, "POST")]
    [InlineData(HttpMethodKind.Put, "PUT")]
    [InlineData(HttpMethodKind.Patch, "PATCH")]
    [InlineData(HttpMethodKind.Delete, "DELETE")]
    [InlineData(HttpMethodKind.Options, "OPTIONS")]
    public async Task Each_method_arrives_with_the_right_verb(HttpMethodKind kind, string verb)
    {
        var result = await _transport.SendAsync(Req("/echo", kind), CancellationToken.None);

        Assert.Equal(verb, Echo(result).GetProperty("method").GetString());
    }

    [Fact]
    public async Task Head_returns_no_body()
    {
        var result = await _transport.SendAsync(Req("/echo", HttpMethodKind.Head), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(200, result.Response!.StatusCode);
        Assert.Empty(result.Response.Body);
    }

    [Fact]
    public async Task Urlencoded_body_is_sent()
    {
        var result = await _transport.SendAsync(
            Req("/echo", HttpMethodKind.Post, "a=1&b=x%20y", "application/x-www-form-urlencoded"), CancellationToken.None);

        var echo = Echo(result);
        Assert.Equal("a=1&b=x%20y", echo.GetProperty("body").GetString());
        Assert.StartsWith("application/x-www-form-urlencoded", echo.GetProperty("headers").GetProperty("Content-Type").GetString());
    }

    [Fact]
    public async Task Multipart_sends_text_and_file_parts()
    {
        var bytes = new byte[] { 0, 1, 2, 255, 254, 10, 13 };
        var file = Path.Combine(_dir, "data.bin");
        await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);
        ResolvedPart[] parts = [new("t", "hello", null), new("f", null, file)];

        var result = await _transport.SendAsync(Req("/echo", HttpMethodKind.Post, parts: parts), CancellationToken.None);

        var echo = Echo(result);
        Assert.Contains("boundary=", echo.GetProperty("headers").GetProperty("Content-Type").GetString());
        var body = Convert.FromBase64String(echo.GetProperty("bodyBase64").GetString()!);
        var text = System.Text.Encoding.Latin1.GetString(body);
        Assert.Contains("name=t", text);
        Assert.Contains("hello", text);
        Assert.Contains("filename=data.bin", text);
        Assert.Contains("application/octet-stream", text);
        Assert.NotEqual(-1, body.AsSpan().IndexOf(bytes));
    }

    [Fact]
    public async Task Binary_file_bytes_are_intact()
    {
        var bytes = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var file = Path.Combine(_dir, "all.bin");
        await File.WriteAllBytesAsync(file, bytes, TestContext.Current.CancellationToken);

        var result = await _transport.SendAsync(
            Req("/echo", HttpMethodKind.Put, contentType: "application/octet-stream", file: file), CancellationToken.None);

        Assert.Equal(bytes, Convert.FromBase64String(Echo(result).GetProperty("bodyBase64").GetString()!));
    }

    [Fact]
    public async Task Unreadable_file_fails_before_any_request()
    {
        var missing = Path.Combine(_dir, "missing.bin");

        var result = await _transport.SendAsync(Req("/echo", HttpMethodKind.Post, file: missing), CancellationToken.None);

        Assert.Equal(SendFailureKind.FileUnreadable, result.Failure!.Kind);
        Assert.StartsWith($"Cannot read file '{missing}': ", result.Failure.Message);
    }

    [Fact]
    public async Task Redirect_is_followed_by_default()
    {
        var result = await _transport.SendAsync(Req("/redirect"), CancellationToken.None);

        Assert.Equal(200, result.Response!.StatusCode);
        Assert.EndsWith("/echo", result.Response.FinalUrl);
    }

    [Fact]
    public async Task Redirect_is_returned_untouched_when_not_followed()
    {
        var result = await _transport.SendAsync(Req("/redirect", follow: false), CancellationToken.None);

        Assert.Equal(302, result.Response!.StatusCode);
        Assert.Contains(result.Response.Headers, h => h.Key == "Location" && h.Value.EndsWith("/echo", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Per_request_timeout_overrides_the_default()
    {
        var result = await _transport.SendAsync(Req("/delay/3000", timeout: 1), CancellationToken.None);

        Assert.Equal(SendFailureKind.Timeout, result.Failure!.Kind);
        Assert.Equal("Request timed out after 1 s", result.Failure.Message);
    }
}
