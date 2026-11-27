using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Yaapic.Core.Mocks;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Mocks;

namespace Yaapic.Infrastructure.Tests.Mocks;

public class TcpMockHttpHostTests
{
    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private static MockHttpResponse Text(string body, TimeSpan? delay = null) =>
        new(200, "OK", [new KeyValueEntry("Content-Type", "text/plain")], Encoding.UTF8.GetBytes(body), delay ?? TimeSpan.Zero);

    [Fact]
    public async Task Get_and_post_reach_the_handler_with_headers_and_body()
    {
        var seen = new List<(string Method, string? Header, string Body)>();
        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        await using var server = await host.StartAsync(port, (r, _) =>
        {
            lock (seen)
            {
                seen.Add((r.Method, r.Headers.FirstOrDefault(h => h.Key == "X-Test")?.Value, Encoding.UTF8.GetString(r.Body)));
            }

            return Task.FromResult(Text("hi"));
        }, TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-Test", "1");

        Assert.Equal("hi", await http.GetStringAsync($"http://127.0.0.1:{port}/a", TestContext.Current.CancellationToken));
        var post = await http.PostAsync($"http://127.0.0.1:{port}/b", new StringContent("payload"), TestContext.Current.CancellationToken);

        Assert.Equal("hi", await post.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(("GET", "1", ""), seen[0]);
        Assert.Equal(("POST", "1", "payload"), seen[1]);
        Assert.Equal(port, server.Port);
    }

    [Fact]
    public async Task Keep_alive_reuses_one_connection()
    {
        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        await using var server = await host.StartAsync(port, (_, _) => Task.FromResult(Text("x")), TestContext.Current.CancellationToken);
        using var http = new HttpClient();

        await http.GetStringAsync($"http://127.0.0.1:{port}/1", TestContext.Current.CancellationToken);
        await http.GetStringAsync($"http://127.0.0.1:{port}/2", TestContext.Current.CancellationToken);

        Assert.Equal(1, host.AcceptedConnections);
    }

    [Fact]
    public async Task Delay_is_applied()
    {
        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        await using var server = await host.StartAsync(
            port, (_, _) => Task.FromResult(Text("x", TimeSpan.FromMilliseconds(300))), TestContext.Current.CancellationToken);
        using var http = new HttpClient();

        var started = System.Diagnostics.Stopwatch.StartNew();
        await http.GetStringAsync($"http://127.0.0.1:{port}/", TestContext.Current.CancellationToken);

        Assert.True(started.Elapsed >= TimeSpan.FromMilliseconds(250), started.Elapsed.ToString());
    }

    [Fact]
    public async Task Port_in_use_throws()
    {
        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        await using var first = await host.StartAsync(port, (_, _) => Task.FromResult(Text("x")), TestContext.Current.CancellationToken);

        var e = await Assert.ThrowsAsync<MockPortInUseException>(
            () => host.StartAsync(port, (_, _) => Task.FromResult(Text("x")), TestContext.Current.CancellationToken));

        Assert.Equal(port, e.Port);
    }

    [Fact]
    public async Task Dispose_stops_answering_and_frees_the_port()
    {
        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        var server = await host.StartAsync(port, (_, _) => Task.FromResult(Text("x")), TestContext.Current.CancellationToken);
        using var http = new HttpClient();
        await http.GetStringAsync($"http://127.0.0.1:{port}/", TestContext.Current.CancellationToken);

        await server.DisposeAsync();

        using var fresh = new HttpClient();
        await Assert.ThrowsAsync<HttpRequestException>(() => fresh.GetStringAsync($"http://127.0.0.1:{port}/", TestContext.Current.CancellationToken));
        await using var again = await host.StartAsync(port, (_, _) => Task.FromResult(Text("y")), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handler_exception_gives_500()
    {
        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        await using var server = await host.StartAsync(
            port, (_, _) => throw new InvalidOperationException("boom"), TestContext.Current.CancellationToken);
        using var http = new HttpClient();

        var response = await http.GetAsync($"http://127.0.0.1:{port}/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Mock server error", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task The_101st_concurrent_connection_gets_503()
    {
        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        await using var server = await host.StartAsync(port, (_, _) => Task.FromResult(Text("x")), TestContext.Current.CancellationToken);
        var idle = new List<TcpClient>();
        try
        {
            for (var i = 0; i < TcpMockHttpHost.MaxConnections; i++)
            {
                var c = new TcpClient();
                await c.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
                idle.Add(c);
            }

            while (host.AcceptedConnections < TcpMockHttpHost.MaxConnections)
            {
                await Task.Delay(10, TestContext.Current.CancellationToken);
            }

            using var last = new TcpClient();
            await last.ConnectAsync(IPAddress.Loopback, port, TestContext.Current.CancellationToken);
            var buffer = new byte[64];
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); cts.CancelAfter(TimeSpan.FromSeconds(10));
            var read = await last.GetStream().ReadAsync(buffer, cts.Token);

            Assert.StartsWith("HTTP/1.1 503", Encoding.Latin1.GetString(buffer, 0, read));
        }
        finally
        {
            foreach (var c in idle)
            {
                c.Dispose();
            }
        }
    }

    [Fact]
    public async Task Is_not_reachable_on_other_interfaces()
    {
        var other = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        if (other is null)
        {
            return;
        }

        var host = new TcpMockHttpHost(TimeProvider.System);
        var port = FreePort();
        await using var server = await host.StartAsync(port, (_, _) => Task.FromResult(Text("x")), TestContext.Current.CancellationToken);
        using var client = new TcpClient();

        await Assert.ThrowsAsync<SocketException>(async () => await client.ConnectAsync(other, port, TestContext.Current.CancellationToken));
    }
}
