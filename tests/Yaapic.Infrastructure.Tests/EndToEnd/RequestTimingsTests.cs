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

public sealed class RequestTimingsTests : IDisposable
{
    private readonly TcpListener _plain = new(IPAddress.Loopback, 0);
    private readonly TcpListener _tls = new(IPAddress.Loopback, 0);
    private readonly X509Certificate2 _serverCertificate = CreateServerCertificate();
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _loops = [];

    public RequestTimingsTests()
    {
        _plain.Start();
        _tls.Start();
        _loops.Add(Task.Run(() => AcceptLoop(_plain, tls: false)));
        _loops.Add(Task.Run(() => AcceptLoop(_tls, tls: true)));
    }

    public void Dispose()
    {
        _stop.Cancel();
        _plain.Stop();
        _tls.Stop();
        try { Task.WaitAll([.. _loops], TimeSpan.FromSeconds(5)); } catch (AggregateException) { }
        _stop.Dispose();
        _serverCertificate.Dispose();
    }

    private static X509Certificate2 CreateServerCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var san = new SubjectAlternativeNameBuilder();
        san.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(san.Build());
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null);
    }

    private async Task AcceptLoop(TcpListener listener, bool tls)
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient tcp;
            try { tcp = await listener.AcceptTcpClientAsync(_stop.Token); }
            catch (Exception) { return; }
            _ = Task.Run(() => Serve(tcp, tls));
        }
    }

    // Keep-alive server: answers every request after 200 ms (headers) and 200 ms (body).
    private async Task Serve(TcpClient tcp, bool tls)
    {
        using var _ = tcp;
        try
        {
            Stream stream = tcp.GetStream();
            if (tls)
            {
                await Task.Delay(300, _stop.Token);
                var ssl = new SslStream(stream);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = _serverCertificate }, _stop.Token);
                stream = ssl;
            }

            await using var disposable = stream;
            var buffer = new byte[4096];
            while (!_stop.IsCancellationRequested)
            {
                var read = 0;
                do
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(read), _stop.Token);
                    if (n == 0)
                        return;
                    read += n;
                }
                while (!Encoding.ASCII.GetString(buffer, 0, read).Contains("\r\n\r\n", StringComparison.Ordinal));

                await Task.Delay(200, _stop.Token);
                await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\n"u8.ToArray(), _stop.Token);
                await stream.FlushAsync(_stop.Token);
                await Task.Delay(200, _stop.Token);
                await stream.WriteAsync("body"u8.ToArray(), _stop.Token);
                await stream.FlushAsync(_stop.Token);
            }
        }
        catch (Exception)
        {
            // A canceled test or a closed client just ends the connection.
        }
    }

    private static string Url(TcpListener listener, string scheme) =>
        $"{scheme}://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/";

    private static ResolvedRequest Req(string url) =>
        new(HttpMethodKind.Get, url, [], null, null, [], null, null, true, 0);

    private sealed class FixedProvider(X509Certificate2 certificate) : IClientCertificateProvider
    {
        public Task<ClientCertificateLookup> FindAsync(Uri url, CancellationToken ct) =>
            Task.FromResult(new ClientCertificateLookup("a", certificate, null));
    }

    private static HttpClientTransport PlainTransport() =>
        new(DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(20));

    [Fact]
    public async Task A_first_request_measures_connect_wait_and_download()
    {
        using var transport = PlainTransport();

        var result = await transport.SendAsync(Req(Url(_plain, "http")), CancellationToken.None);

        var timings = result.Response!.Timings!;
        Assert.False(timings.ConnectionReused);
        Assert.NotNull(timings.Dns);
        Assert.NotNull(timings.Connect);
        Assert.Null(timings.Tls);
        Assert.True(timings.Wait >= TimeSpan.FromMilliseconds(180), $"Wait {timings.Wait}");
        Assert.True(timings.Download >= TimeSpan.FromMilliseconds(180), $"Download {timings.Download}");
        Assert.True(timings.Sum <= result.Response.Duration, $"Sum {timings.Sum} > Duration {result.Response.Duration}");
    }

    [Fact]
    public async Task A_second_request_on_the_pooled_connection_is_reused()
    {
        using var transport = PlainTransport();
        var url = Url(_plain, "http");
        await transport.SendAsync(Req(url), CancellationToken.None);

        var result = await transport.SendAsync(Req(url), CancellationToken.None);

        var timings = result.Response!.Timings!;
        Assert.True(timings.ConnectionReused);
        Assert.Null(timings.Dns);
        Assert.Null(timings.Connect);
        Assert.Null(timings.Tls);
        Assert.True(timings.Wait >= TimeSpan.FromMilliseconds(180));
    }

    [Fact]
    public async Task A_slow_tls_handshake_is_measured()
    {
        using var transport = new HttpClientTransport(
            DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(20),
            new FixedProvider(_serverCertificate),
            handler => handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true);

        var result = await transport.SendAsync(Req(Url(_tls, "https")), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Failure?.Message);
        var timings = result.Response!.Timings!;
        Assert.True(timings.Tls >= TimeSpan.FromMilliseconds(250), $"Tls {timings.Tls}");
        Assert.True(timings.Sum <= result.Response.Duration);
    }

    [Fact]
    public async Task A_self_signed_server_still_fails_validation()
    {
        using var transport = PlainTransport();

        var result = await transport.SendAsync(Req(Url(_tls, "https")), CancellationToken.None);

        Assert.Equal(SendFailureKind.ConnectionFailed, result.Failure?.Kind);
    }

    [Fact]
    public async Task A_failure_has_no_timings()
    {
        using var transport = PlainTransport();
        var port = ((IPEndPoint)_plain.LocalEndpoint).Port;
        _plain.Stop();

        var result = await transport.SendAsync(Req($"http://127.0.0.1:{port}/"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Response);
    }
}
