using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Yaapic.Core.Certificates;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Http;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

public sealed class ClientCertificateTransportTests : IDisposable
{
    private readonly X509Certificate2 _server = Create("CN=localhost", server: true);
    private readonly X509Certificate2 _client = Create("CN=yaapic-client", server: false);
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;
    private int _handshakes;

    public ClientCertificateTransportTests()
    {
        _listener.Start();
        _loop = Task.Run(AcceptLoop);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        try { _loop.Wait(TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
        _stop.Dispose();
        _server.Dispose();
        _client.Dispose();
    }

    private static X509Certificate2 Create(string subject, bool server)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid(server ? "1.3.6.1.5.5.7.3.1" : "1.3.6.1.5.5.7.3.2")], false));
        if (server)
        {
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
        }

        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        // Round-trip through PFX so the private key is usable by Schannel.
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null);
    }

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient tcp;
            try { tcp = await _listener.AcceptTcpClientAsync(_stop.Token); }
            catch (Exception) { return; }
            _ = Task.Run(() => Serve(tcp));
        }
    }

    private async Task Serve(TcpClient tcp)
    {
        using var _ = tcp;
        try
        {
            await using var ssl = new SslStream(tcp.GetStream());
            await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = _server,
                ClientCertificateRequired = true,
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            }, _stop.Token);
            Interlocked.Increment(ref _handshakes);

            var buffer = new byte[4096];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await ssl.ReadAsync(buffer.AsMemory(read), _stop.Token);
                if (n == 0)
                    break;
                read += n;
                if (Encoding.ASCII.GetString(buffer, 0, read).Contains("\r\n\r\n", StringComparison.Ordinal))
                    break;
            }

            var thumbprint = ssl.RemoteCertificate is { } remote ? new X509Certificate2(remote).Thumbprint : "none";
            var response = $"HTTP/1.1 200 OK\r\nContent-Length: {thumbprint.Length}\r\nConnection: close\r\n\r\n{thumbprint}";
            await ssl.WriteAsync(Encoding.ASCII.GetBytes(response), _stop.Token);
            await ssl.FlushAsync(_stop.Token);
        }
        catch (Exception)
        {
            // A failed handshake or a canceled test just ends the connection.
        }
    }

    private string Url => $"https://localhost:{((IPEndPoint)_listener.LocalEndpoint).Port}/";

    private static ResolvedRequest Req(string url) =>
        new(HttpMethodKind.Get, url, [], null, null, [], null, null, true, 0);

    private static void AcceptAnyServer(SocketsHttpHandler handler) =>
        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

    private static HttpClient PlainClient()
    {
        var handler = new SocketsHttpHandler();
        AcceptAnyServer(handler);
        return new HttpClient(handler);
    }

    private HttpClientTransport Transport(IClientCertificateProvider provider) => new(
        PlainClient(), PlainClient(), TimeSpan.FromSeconds(20), provider, AcceptAnyServer);

    private sealed class FakeProvider(Func<Uri, ClientCertificateLookup> lookup) : IClientCertificateProvider
    {
        public int Calls { get; private set; }

        public Task<ClientCertificateLookup> FindAsync(Uri url, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(lookup(url));
        }
    }

    [Fact]
    public async Task A_matching_entry_presents_the_client_certificate()
    {
        using var transport = Transport(new FakeProvider(_ => new("a", _client, null)));

        var result = await transport.SendAsync(Req(Url), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal(_client.Thumbprint, Encoding.UTF8.GetString(result.Response!.Body));
    }

    [Fact]
    public async Task A_url_without_a_matching_entry_sends_no_certificate()
    {
        using var transport = Transport(new FakeProvider(_ => new(null, null, null)));

        var result = await transport.SendAsync(Req(Url), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("none", Encoding.UTF8.GetString(result.Response!.Body));
        Assert.Equal(0, transport.CachedCertificateClients);
    }

    [Fact]
    public async Task A_lookup_error_fails_without_connecting()
    {
        using var transport = Transport(new FakeProvider(_ => new("a", null, "bad passphrase")));

        var result = await transport.SendAsync(Req(Url), CancellationToken.None);

        Assert.Equal(SendFailureKind.ClientCertificateError, result.Failure?.Kind);
        Assert.Equal("bad passphrase", result.Failure?.Message);
        Assert.Equal(0, _handshakes);
    }

    [Fact]
    public async Task Two_sends_reuse_the_cached_clients()
    {
        using var transport = Transport(new FakeProvider(_ => new("a", _client, null)));

        await transport.SendAsync(Req(Url), CancellationToken.None);
        await transport.SendAsync(Req(Url), CancellationToken.None);

        Assert.Equal(1, transport.CachedCertificateClients);
    }

    [Fact]
    public async Task A_different_certificate_instance_replaces_the_cached_clients()
    {
        var current = _client;
        using var other = Create("CN=other", server: false);
        using var transport = Transport(new FakeProvider(_ => new("a", current, null)));

        await transport.SendAsync(Req(Url), CancellationToken.None);
        current = other;
        var result = await transport.SendAsync(Req(Url), CancellationToken.None);

        Assert.Equal(1, transport.CachedCertificateClients);
        Assert.Equal(other.Thumbprint, Encoding.UTF8.GetString(result.Response!.Body));
    }
}
