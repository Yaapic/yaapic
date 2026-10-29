using System.Net;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Http;

namespace Yaapic.Infrastructure.Tests;

public class HttpClientTransportTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        public HttpRequestMessage? Last { get; private set; }

        public byte[]? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Last = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(ct);
            return await respond(request, ct);
        }
    }

    private static StubHandler Ok(HttpStatusCode status = HttpStatusCode.OK) =>
        new((r, _) => Task.FromResult(new HttpResponseMessage(status) { RequestMessage = r, Content = new StringContent("hi") }));

    private static StubHandler Hanging() =>
        new(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage();
        });

    private static HttpClientTransport Create(StubHandler h, TimeSpan? timeout = null) =>
        new(new HttpClient(h), timeout ?? TimeSpan.FromSeconds(5));

    private static ResolvedRequest Req(string url, HttpMethodKind m = HttpMethodKind.Get, KeyValueEntry[]? headers = null,
        string? body = null, string? contentType = null) =>
        new(m, url, headers ?? [], body, contentType, []);

    [Fact]
    public async Task Get_maps_status_headers_body_and_final_url()
    {
        var h = new StubHandler((r, _) =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = r, ReasonPhrase = "Fine", Content = new StringContent("hello") };
            resp.Headers.Add("X-A", "1");
            return Task.FromResult(resp);
        });
        var result = await Create(h).SendAsync(Req("https://example.com/a"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var r = result.Response!;
        Assert.Equal(200, r.StatusCode);
        Assert.Equal("Fine", r.ReasonPhrase);
        Assert.Equal("hello"u8.ToArray(), r.Body);
        Assert.Equal("https://example.com/a", r.FinalUrl);
        Assert.Contains(r.Headers, e => e.Key == "X-A" && e.Value == "1");
        Assert.Contains(r.Headers, e => e.Key == "Content-Type");
    }

    [Fact]
    public async Task Post_sends_body_with_content_type()
    {
        var h = Ok();
        await Create(h).SendAsync(Req("https://e.com", HttpMethodKind.Post, body: "{\"a\":1}", contentType: "application/json"), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, h.Last!.Method);
        Assert.Equal("{\"a\":1}"u8.ToArray(), h.LastBody);
        Assert.Equal("application/json", h.Last.Content!.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task User_headers_reach_the_request_and_content_type_override_applies()
    {
        var h = Ok();
        var headers = new[]
        {
            new KeyValueEntry("X-Test", "v"),
            new KeyValueEntry("Content-Length", "5"),
            new KeyValueEntry("Host", "evil"),
            new KeyValueEntry("Content-Language", "fr"),
        };
        await Create(h).SendAsync(Req("https://e.com", HttpMethodKind.Post, headers, "x", "text/plain"), CancellationToken.None);

        Assert.Equal("v", h.Last!.Headers.GetValues("X-Test").Single());
        Assert.Equal("fr", h.Last.Content!.Headers.GetValues("Content-Language").Single());
        Assert.NotEqual("evil", h.Last.Headers.Host);
        Assert.Equal("text/plain", h.Last.Content.Headers.ContentType!.MediaType);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    public async Task Invalid_url_fails_without_calling_the_handler(string url)
    {
        var h = Ok();
        var result = await Create(h).SendAsync(Req(url), CancellationToken.None);

        Assert.Equal(SendFailureKind.InvalidUrl, result.Failure!.Kind);
        Assert.Equal($"Invalid URL: {url}", result.Failure.Message);
        Assert.Equal(0, h.Calls);
    }

    [Fact]
    public async Task HttpRequestException_is_a_connection_failure()
    {
        var h = new StubHandler((_, _) => throw new HttpRequestException("outer", new IOException("refused")));
        var result = await Create(h).SendAsync(Req("https://e.com"), CancellationToken.None);

        Assert.Equal(SendFailureKind.ConnectionFailed, result.Failure!.Kind);
        Assert.Equal("refused", result.Failure.Message);
    }

    [Fact]
    public async Task Slow_handler_times_out()
    {
        var result = await Create(Hanging(), TimeSpan.FromMilliseconds(50)).SendAsync(Req("https://e.com"), CancellationToken.None);

        Assert.Equal(SendFailureKind.Timeout, result.Failure!.Kind);
    }

    [Fact]
    public async Task Caller_cancellation_is_canceled()
    {
        using var cts = new CancellationTokenSource(50);
        var result = await Create(Hanging()).SendAsync(Req("https://e.com"), cts.Token);

        Assert.Equal(SendFailureKind.Canceled, result.Failure!.Kind);
        Assert.Equal("Request canceled", result.Failure.Message);
    }

    [Fact]
    public async Task Http_404_is_a_success()
    {
        var result = await Create(Ok(HttpStatusCode.NotFound)).SendAsync(Req("https://e.com"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(404, result.Response!.StatusCode);
    }

    [Fact]
    public void Default_client_has_infinite_timeout()
    {
        using var client = DefaultHttpClient.Create();
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }
}
