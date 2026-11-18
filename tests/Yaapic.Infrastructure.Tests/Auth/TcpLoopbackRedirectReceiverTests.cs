using System.Net;
using System.Net.Sockets;
using System.Text;
using Yaapic.Infrastructure.Auth;

namespace Yaapic.Infrastructure.Tests.Auth;

public sealed class TcpLoopbackRedirectReceiverTests
{
    private const string Html = "<p>Done é</p>";
    private readonly TcpLoopbackRedirectReceiver _receiver = new();

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task<string> SendAsync(int port, string raw)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                var stream = client.GetStream();
                await stream.WriteAsync(Encoding.ASCII.GetBytes(raw));
                using var reader = new MemoryStream();
                await stream.CopyToAsync(reader);
                return Encoding.UTF8.GetString(reader.ToArray());
            }
            catch (SocketException) when (attempt < 50)
            {
                await Task.Delay(20); // the listener is not up yet
            }
        }
    }

    private static string Get(int port, string target, string? host = null) =>
        $"GET {target} HTTP/1.1\r\nHost: {host ?? $"127.0.0.1:{port}"}\r\n\r\n";

    [Fact]
    public async Task Returns_the_target_with_its_query_and_answers_the_page()
    {
        var port = FreePort();
        var wait = _receiver.WaitForRequestTargetAsync(port, "/callback", Html, CancellationToken.None);

        var response = await SendAsync(port, Get(port, "/callback?code=abc&state=xyz", $"localhost:{port}"));

        Assert.Equal("/callback?code=abc&state=xyz", await wait);
        Assert.StartsWith("HTTP/1.1 200 OK", response);
        Assert.Contains("Content-Type: text/html; charset=utf-8", response);
        Assert.Contains($"Content-Length: {Encoding.UTF8.GetByteCount(Html)}", response);
        Assert.EndsWith(Html, response);
    }

    [Fact]
    public async Task Ignores_other_paths_and_a_trailing_slash_matches()
    {
        var port = FreePort();
        var wait = _receiver.WaitForRequestTargetAsync(port, "/callback", Html, CancellationToken.None);

        var other = await SendAsync(port, Get(port, "/favicon.ico"));
        var post = await SendAsync(port, $"POST /callback HTTP/1.1\r\nHost: 127.0.0.1:{port}\r\n\r\n");
        await SendAsync(port, Get(port, "/callback/?code=1"));

        Assert.StartsWith("HTTP/1.1 404", other);
        Assert.StartsWith("HTTP/1.1 404", post);
        Assert.Equal("/callback/?code=1", await wait);
    }

    [Fact]
    public async Task Rejects_a_foreign_host_malformed_and_oversize_requests()
    {
        var port = FreePort();
        var wait = _receiver.WaitForRequestTargetAsync(port, "/callback", Html, CancellationToken.None);

        var foreign = await SendAsync(port, Get(port, "/callback?code=1", "evil.example:80"));
        var malformed = await SendAsync(port, "NONSENSE\r\n\r\n");
        var oversize = await SendAsync(port, Get(port, "/callback?" + new string('a', 9000)));

        Assert.StartsWith("HTTP/1.1 400", foreign);
        Assert.StartsWith("HTTP/1.1 400", malformed);
        Assert.StartsWith("HTTP/1.1 400", oversize);
        Assert.False(wait.IsCompleted);

        await SendAsync(port, Get(port, "/callback?code=ok"));
        Assert.Equal("/callback?code=ok", await wait);
    }

    [Fact]
    public async Task Cancellation_stops_the_listener_and_frees_the_port()
    {
        var port = FreePort();
        using var cts = new CancellationTokenSource();
        var wait = _receiver.WaitForRequestTargetAsync(port, "/callback", Html, cts.Token);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
        var again = new TcpListener(IPAddress.Loopback, port);
        again.Start();
        again.Stop();
    }

    [Fact]
    public async Task A_busy_port_gives_the_localized_error()
    {
        var port = FreePort();
        var holder = new TcpListener(IPAddress.Loopback, port);
        holder.Start();
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _receiver.WaitForRequestTargetAsync(port, "/callback", Html, CancellationToken.None));

            Assert.Contains(port.ToString(), ex.Message);
        }
        finally
        {
            holder.Stop();
        }
    }
}
