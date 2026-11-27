using System.Text;
using Yaapic.Core.Mocks;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Mocks;

namespace Yaapic.Infrastructure.Tests.Mocks;

public class Http11Tests
{
    private sealed class Duplex(byte[] input, Stream output) : MemoryStream(input)
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) => output.WriteAsync(buffer, ct);
    }

    private static Http11RequestReader Reader(string raw, out MemoryStream stream, Http11Limits? limits = null)
    {
        stream = new MemoryStream(Encoding.Latin1.GetBytes(raw));
        return new Http11RequestReader(stream, limits ?? Http11Limits.Default);
    }

    private static async Task<Http11ReadResult.Request> Req(string raw)
    {
        using var reader = Reader(raw, out _);
        return Assert.IsType<Http11ReadResult.Request>(await reader.ReadAsync(CancellationToken.None));
    }

    private static async Task<int> Err(string raw, Http11Limits? limits = null)
    {
        using var reader = Reader(raw, out _, limits);
        return Assert.IsType<Http11ReadResult.Error>(await reader.ReadAsync(CancellationToken.None)).Status;
    }

    [Fact]
    public async Task SimpleGet()
    {
        var r = await Req("GET /a?x=1 HTTP/1.1\r\nHost: h\r\n\r\n");
        Assert.Equal("GET", r.Value.Method);
        Assert.Equal("/a", r.Value.Path);
        Assert.Equal("h", r.Value.Header("host"));
        Assert.Empty(r.Value.Body);
        Assert.True(r.KeepAlive);
    }

    [Fact]
    public async Task PostWithContentLength()
    {
        var r = await Req("POST /a HTTP/1.1\r\nContent-Length: 5\r\n\r\nhello");
        Assert.Equal("hello", Encoding.UTF8.GetString(r.Value.Body));
    }

    [Fact]
    public async Task ChunkedWithExtensionsAndTrailers()
    {
        var r = await Req("POST /a HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n3;ext=1\r\nabc\r\n2\r\nde\r\n0\r\nX-T: 1\r\n\r\n");
        Assert.Equal("abcde", Encoding.UTF8.GetString(r.Value.Body));
    }

    [Fact]
    public async Task ExpectContinueWritesInterimResponse()
    {
        var written = new MemoryStream();
        using var reader = new Http11RequestReader(
            new Duplex(Encoding.Latin1.GetBytes("POST /a HTTP/1.1\r\nExpect: 100-continue\r\nContent-Length: 2\r\n\r\nhi"), written),
            Http11Limits.Default);
        var result = Assert.IsType<Http11ReadResult.Request>(await reader.ReadAsync(CancellationToken.None));
        Assert.Equal("hi", Encoding.UTF8.GetString(result.Value.Body));
        Assert.Equal("HTTP/1.1 100 Continue\r\n\r\n", Encoding.Latin1.GetString(written.ToArray()));
    }

    [Fact]
    public async Task PipelinedRequestsAreReadInOrder()
    {
        using var reader = Reader("GET /1 HTTP/1.1\r\n\r\nPOST /2 HTTP/1.1\r\nContent-Length: 1\r\n\r\nx", out _);
        var a = Assert.IsType<Http11ReadResult.Request>(await reader.ReadAsync(CancellationToken.None));
        var b = Assert.IsType<Http11ReadResult.Request>(await reader.ReadAsync(CancellationToken.None));
        Assert.Equal("/1", a.Value.Path);
        Assert.Equal("/2", b.Value.Path);
        Assert.IsType<Http11ReadResult.Closed>(await reader.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task BareLfIsAccepted()
    {
        var r = await Req("GET / HTTP/1.1\nHost: h\n\n");
        Assert.Equal("h", r.Value.Header("Host"));
    }

    [Fact]
    public async Task ErrorStatuses()
    {
        Assert.Equal(400, await Err("GARBAGE\r\n\r\n"));
        Assert.Equal(400, await Err("GET / HTTP/1.1\r\n folded\r\n\r\n"));
        Assert.Equal(400, await Err("POST / HTTP/1.1\r\nContent-Length: 1\r\nContent-Length: 2\r\n\r\nx"));
        Assert.Equal(431, await Err("GET / HTTP/1.1\r\n" + string.Concat(Enumerable.Range(0, 101).Select(i => $"H{i}: v\r\n")) + "\r\n"));
        Assert.Equal(431, await Err("GET / HTTP/1.1\r\nX: " + new string('a', 70 * 1024) + "\r\n\r\n"));
        Assert.Equal(413, await Err($"POST / HTTP/1.1\r\nContent-Length: {11 * 1024 * 1024}\r\n\r\n"));
        Assert.Equal(505, await Err("GET / HTTP/2.0\r\n\r\n"));
        Assert.Equal(501, await Err("POST / HTTP/1.1\r\nTransfer-Encoding: gzip\r\n\r\n"));
    }

    [Theory]
    [InlineData("HTTP/1.1", "", true)]
    [InlineData("HTTP/1.1", "Connection: close\r\n", false)]
    [InlineData("HTTP/1.0", "", false)]
    [InlineData("HTTP/1.0", "Connection: keep-alive\r\n", true)]
    public async Task KeepAliveRules(string version, string header, bool expected)
    {
        var r = await Req($"GET / {version}\r\n{header}\r\n");
        Assert.Equal(expected, r.KeepAlive);
    }

    private static async Task<string> Write(MockHttpResponse response)
    {
        using var ms = new MemoryStream();
        await Http11ResponseWriter.WriteAsync(ms, response, true, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), CancellationToken.None);
        return Encoding.Latin1.GetString(ms.ToArray());
    }

    private const string Tail = "Date: Fri, 02 Jan 2026 03:04:05 GMT\r\nServer: Yaapic-Mock\r\nConnection: keep-alive\r\n\r\n";

    [Fact]
    public async Task WritesJsonResponse()
    {
        var text = await Write(new MockHttpResponse(
            200, "OK", [new("Content-Type", "application/json")], "{}"u8.ToArray(), TimeSpan.Zero));
        Assert.Equal("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n" + Tail + "{}", text);
    }

    [Fact]
    public async Task HeadKeepsLengthWithoutBody()
    {
        var text = await Write(new MockHttpResponse(200, "OK", [], "abc"u8.ToArray(), TimeSpan.Zero, true));
        Assert.Equal("HTTP/1.1 200 OK\r\nContent-Length: 3\r\n" + Tail, text);
    }

    [Fact]
    public async Task NoContentSendsNeitherBodyNorLength()
    {
        var text = await Write(new MockHttpResponse(204, "No Content", [], "abc"u8.ToArray(), TimeSpan.Zero));
        Assert.Equal("HTTP/1.1 204 No Content\r\n" + Tail, text);
    }

    [Fact]
    public async Task RouteOwnedHeadersAreIgnoredAndCrLfNeutralised()
    {
        var text = await Write(new MockHttpResponse(
            200, "OK", [new("Content-Length", "99"), new("Connection", "upgrade"), new("X-A", "a\r\nInjected: 1")], [], TimeSpan.Zero));
        Assert.Equal("HTTP/1.1 200 OK\r\nX-A: a  Injected: 1\r\nContent-Length: 0\r\n" + Tail, text);
    }

    [Fact]
    public async Task ErrorResponseClosesTheConnection()
    {
        using var ms = new MemoryStream();
        await Http11ResponseWriter.WriteErrorAsync(ms, 413, CancellationToken.None);
        var text = Encoding.Latin1.GetString(ms.ToArray());
        Assert.StartsWith("HTTP/1.1 413 Content Too Large\r\n", text, StringComparison.Ordinal);
        Assert.Contains("Connection: close", text, StringComparison.Ordinal);
    }
}
