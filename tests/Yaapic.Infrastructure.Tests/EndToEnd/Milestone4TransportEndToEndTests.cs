using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Diff;
using Yaapic.Core.Http;
using Yaapic.Core.Secrets;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Certificates;
using Yaapic.Infrastructure.Http;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>
/// mTLS, timings, HTTP version, response diff and code generation through the composed services: real certificate store,
/// passphrase vault, certificate provider and transport against local servers, then the real view models.
/// </summary>
public sealed class Milestone4TransportEndToEndTests : IDisposable
{
    private const string Passphrase = "vault passphrase";
    private const string PfxPassphrase = "pfx passphrase";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-e2e4t-" + Guid.NewGuid().ToString("N"));
    private readonly X509Certificate2 _server = Create("CN=localhost", server: true);
    private readonly X509Certificate2 _client;
    private readonly byte[] _clientPfx;
    private readonly TcpListener _plain = new(IPAddress.Loopback, 0);
    private readonly TcpListener _tls = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly List<Task> _loops = [];
    private readonly FileCertificateStore _store;
    private readonly FileSecretVault _vault;
    private readonly ClientCertificateProvider _provider;
    private readonly HttpClientTransport _transport;
    private readonly HttpClientTransport _plainTransport;
    private readonly Guid _entryId = Guid.NewGuid();
    private volatile string _body = "one\n";

    public Milestone4TransportEndToEndTests()
    {
        _client = Create("CN=yaapic-client", server: false, PfxPassphrase, out _clientPfx);
        var workspace = Path.Combine(_dir, "ws");
        Workspace.EnsureCreated(workspace);
        _store = new FileCertificateStore(Path.Combine(_dir, "user"));
        _vault = new FileSecretVault(workspace, new ScriptedShellFixture.TempVaultLocation(Path.Combine(_dir, "vaults")), new PassphraseKeyProtectorFactory(1000));
        _provider = new ClientCertificateProvider(_store, _vault);
        _transport = new HttpClientTransport(
            PlainClient(), PlainClient(), TimeSpan.FromSeconds(20), _provider, AcceptAnyServer, new FakeSupport(false));
        // The instrumented default clients measure timings; the certificate transport above wraps its own handlers.
        _plainTransport = new HttpClientTransport(
            DefaultHttpClient.Create(), DefaultHttpClient.CreateNonRedirecting(), TimeSpan.FromSeconds(20), support: new FakeSupport(false));
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
        _transport.Dispose();
        _plainTransport.Dispose();
        _provider.Dispose();
        _stop.Dispose();
        _server.Dispose();
        _client.Dispose();
        TempWorkspace.Delete(_dir);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class FakeSupport(bool http3) : IHttpVersionSupport
    {
        public bool IsHttp3Supported => http3;
    }

    private static void AcceptAnyServer(SocketsHttpHandler handler) =>
        handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

    // Requests without a certificate go through these clients: the self-signed server must be accepted there too.
    private static HttpClient PlainClient()
    {
        var handler = new SocketsHttpHandler();
        AcceptAnyServer(handler);
        return new HttpClient(handler);
    }

    private static X509Certificate2 Create(string subject, bool server) => Create(subject, server, null, out _);

    private static X509Certificate2 Create(string subject, bool server, string? pfxPassphrase, out byte[] pfx)
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
        pfx = ephemeral.Export(X509ContentType.Pfx, pfxPassphrase);
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

    // Keep-alive server. /slow answers after 200 ms (headers) and 200 ms (body); the TLS listener echoes the client
    // certificate thumbprint ("none" without one); /bin answers binary; anything else answers the current body.
    private async Task Serve(TcpClient tcp, bool tls)
    {
        using var _ = tcp;
        try
        {
            Stream stream = tcp.GetStream();
            if (tls)
            {
                var ssl = new SslStream(stream);
                await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = _server,
                    ClientCertificateRequired = true,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                }, _stop.Token);
                stream = ssl;
            }

            await using var disposable = stream;
            var buffer = new byte[8192];
            while (!_stop.IsCancellationRequested)
            {
                var read = 0;
                string head;
                do
                {
                    var n = await stream.ReadAsync(buffer.AsMemory(read), _stop.Token);
                    if (n == 0)
                        return;
                    read += n;
                    head = Encoding.ASCII.GetString(buffer, 0, read);
                }
                while (!head.Contains("\r\n\r\n", StringComparison.Ordinal));

                var slow = head.StartsWith("GET /slow", StringComparison.Ordinal);
                byte[] body;
                var type = "text/plain";
                if (tls)
                {
                    var thumbprint = stream is SslStream { RemoteCertificate: { } remote } ? new X509Certificate2(remote).Thumbprint : "none";
                    body = Encoding.ASCII.GetBytes(thumbprint);
                }
                else if (head.StartsWith("GET /bin", StringComparison.Ordinal))
                {
                    body = [0x00, 0xFF, 0x01, 0xFE];
                    type = "application/octet-stream";
                }
                else
                {
                    body = Encoding.UTF8.GetBytes(_body);
                }

                if (slow)
                    await Task.Delay(200, _stop.Token);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\n\r\n"), _stop.Token);
                await stream.FlushAsync(_stop.Token);
                if (slow)
                    await Task.Delay(200, _stop.Token);
                await stream.WriteAsync(body, _stop.Token);
                await stream.FlushAsync(_stop.Token);
            }
        }
        catch (Exception)
        {
            // A canceled test or a closed client just ends the connection.
        }
    }

    private static int Port(TcpListener listener) => ((IPEndPoint)listener.LocalEndpoint).Port;

    private string PlainUrl(string path = "/") => $"http://127.0.0.1:{Port(_plain)}{path}";

    private string TlsUrl(string host = "localhost") => $"https://{host}:{Port(_tls)}/";

    private static ResolvedRequest Req(string url, HttpVersionPreference version = HttpVersionPreference.Default) =>
        new(HttpMethodKind.Get, url, [], null, null, [], HttpVersion: version);

    private async Task<SendResult> SendAsync(string url, HttpVersionPreference version = HttpVersionPreference.Default) =>
        await (url.StartsWith("http:", StringComparison.Ordinal) ? _plainTransport : _transport).SendAsync(Req(url, version), Ct);

    /// <summary>Stores a PFX entry (with a vault passphrase) for <c>localhost</c> and unlocks the vault.</summary>
    private async Task AddEntryAsync()
    {
        var pfx = Path.Combine(_dir, "client.pfx");
        Directory.CreateDirectory(_dir);
        await File.WriteAllBytesAsync(pfx, _clientPfx, Ct);
        await _vault.CreateAsync(PassphraseKeyProtector.ProtectorId, Passphrase, Ct);
        await _vault.SetAsync(SecretRef.Certificate(_entryId), PfxPassphrase, Ct);
        await _store.SaveAsync([new ClientCertificate(_entryId, "client", "localhost", ClientCertificateFormat.Pfx, pfx, "", true)], Ct);
    }

    [Fact]
    public async Task A_matching_host_sends_the_certificate_and_a_non_matching_host_does_not()
    {
        await AddEntryAsync();

        var matching = await SendAsync(TlsUrl());
        var other = await SendAsync(TlsUrl("127.0.0.1"));

        Assert.True(matching.IsSuccess, matching.Failure?.Message);
        Assert.Equal(_client.Thumbprint, Encoding.UTF8.GetString(matching.Response!.Body));
        Assert.True(other.IsSuccess, other.Failure?.Message);
        Assert.Equal("none", Encoding.UTF8.GetString(other.Response!.Body));
    }

    [Fact]
    public async Task Locking_the_vault_fails_with_the_vault_message()
    {
        await AddEntryAsync();
        Assert.True((await SendAsync(TlsUrl())).IsSuccess);

        // The certificate is cached in memory: a store change forces the passphrase to be read again.
        _vault.Lock();
        await _store.SaveAsync(await _store.LoadAsync(Ct), Ct);
        var result = await SendAsync(TlsUrl());

        Assert.Equal(SendFailureKind.ClientCertificateError, result.Failure?.Kind);
        Assert.Contains("client", result.Failure!.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(PfxPassphrase, result.Failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Removing_the_entry_stops_sending_the_certificate()
    {
        await AddEntryAsync();
        Assert.Equal(_client.Thumbprint, Encoding.UTF8.GetString((await SendAsync(TlsUrl())).Response!.Body));

        await _store.SaveAsync([], Ct);
        var result = await SendAsync(TlsUrl());

        Assert.True(result.IsSuccess, result.Failure?.Message);
        Assert.Equal("none", Encoding.UTF8.GetString(result.Response!.Body));
    }

    [Fact]
    public async Task Timings_cover_every_phase_on_a_new_connection_then_report_reuse_and_reach_the_panel()
    {
        var first = await SendAsync(PlainUrl("/slow"));
        var second = await SendAsync(PlainUrl("/slow"));

        var t1 = first.Response!.Timings!;
        Assert.False(t1.ConnectionReused);
        Assert.NotNull(t1.Connect);
        Assert.True(t1.Wait >= TimeSpan.FromMilliseconds(150), $"Wait {t1.Wait}");
        Assert.True(t1.Download >= TimeSpan.FromMilliseconds(150), $"Download {t1.Download}");
        Assert.True(t1.Sum <= first.Response.Duration);
        var t2 = second.Response!.Timings!;
        Assert.True(t2.ConnectionReused);
        Assert.True(t2.Sum <= second.Response.Duration);

        var panel = new ResponsePanelViewModel();
        panel.ShowReport(new SendReport(first, []));
        Assert.True(panel.HasTimings);
        Assert.Contains(panel.Timings, r => r.IsMeasured);
        Assert.NotEqual("", panel.TotalText);
        panel.ShowReport(new SendReport(second, []));
        Assert.Equal(Yaapic.Localization.Strings.TimingConnectionReused, panel.ConnectionText);
        Assert.Equal("HTTP/1.1", panel.ProtocolText);
    }

    [Fact]
    public async Task Http1_reports_HTTP_1_1_and_unsupported_Http3_is_sent_as_HTTP_2_or_lower()
    {
        var http1 = await SendAsync(PlainUrl(), HttpVersionPreference.Http1);
        var http3 = await SendAsync(PlainUrl(), HttpVersionPreference.Http3);

        Assert.Equal("HTTP/1.1", http1.Response!.Protocol);
        Assert.True(http3.IsSuccess, http3.Failure?.Message);
        Assert.NotEqual("HTTP/3", http3.Response!.Protocol);
    }

    [Fact]
    public async Task Diff_compares_two_sends_keeps_the_pinned_baseline_and_ignores_a_binary_response()
    {
        var panel = new ResponsePanelViewModel();

        panel.ShowReport(new SendReport(await SendAsync(PlainUrl()), []));
        Assert.False(panel.Diff.HasComparison);
        panel.Diff.PinCommand.Execute(null);
        _body = "one\ntwo\n";
        panel.ShowReport(new SendReport(await SendAsync(PlainUrl()), []));

        Assert.True(panel.Diff.HasComparison);
        Assert.Contains(panel.Diff.BodyLines, l => l.Op == DiffOp.Added && l.Text == "two");
        Assert.DoesNotContain(panel.Diff.BodyLines, l => l.Op == DiffOp.Removed);

        panel.ShowReport(new SendReport(await SendAsync(PlainUrl("/bin")), []));

        Assert.NotNull(panel.Diff.UnavailableReason);
        Assert.Contains("two", panel.Diff.Current!.BodyText, StringComparison.Ordinal);
        Assert.True(panel.Diff.HasBaseline);
        Assert.Contains("one", panel.Diff.Baseline!.BodyText, StringComparison.Ordinal);
    }

    private static CodeGenerationViewModel CodeGeneration(HttpRequestDefinition request, EnvironmentDefinition environment)
    {
        var scopes = new FakeVariableScopeProvider { Scopes = new VariableScopes(environment, [], []) };
        var services = new CodeGenerationServices(
            new CodeGeneratorRegistry(
            [
                new CurlCodeGenerator(), new JavaScriptFetchCodeGenerator(), new PythonRequestsCodeGenerator(),
                new CSharpHttpClientCodeGenerator(), new PowerShellCodeGenerator(), new GoCodeGenerator(),
            ]),
            new InMemoryPreferencesStore(),
            new FakeDialogService());
        var editor = RequestEditorViewModel.FromDefinition(
            request, new RequestSender(new FakeHttpTransport((_, _) => throw new InvalidOperationException("not sent"))), scopes, codeGeneration: services);
        return editor.CreateCodeGeneration();
    }

    [Fact]
    public void Code_generation_contains_the_resolved_url_authorization_and_body_in_all_six_languages()
    {
        var environment = EnvironmentDefinition.Create("dev") with
        {
            Variables = [new("host", "api.example.test"), new("user", "alice"), new("pass", "s3cret")],
        };
        var request = HttpRequestDefinition.Create("codegen") with
        {
            Method = HttpMethodKind.Post,
            Url = "https://{{host}}/items",
            Auth = RequestAuth.None with { Kind = AuthKind.Basic, Username = "{{user}}", Password = "{{pass}}" },
            Body = new RequestBody(BodyKind.Raw, RawContentType.Json, """{"name":"widget"}"""),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret"));
        var vm = CodeGeneration(request, environment);

        Assert.Equal(6, vm.Targets.Count);
        foreach (var target in vm.Targets)
        {
            vm.SelectedTarget = target;

            Assert.Null(vm.Error);
            Assert.Contains("https://api.example.test/items", vm.Code, StringComparison.Ordinal);
            Assert.Contains("Authorization", vm.Code, StringComparison.Ordinal);
            Assert.Contains("Basic " + basic, vm.Code, StringComparison.Ordinal);
            Assert.Contains("widget", vm.Code, StringComparison.Ordinal);
            Assert.DoesNotContain("{{", vm.Code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Code_generation_of_an_OAuth2_request_uses_the_placeholder_header_and_no_real_token()
    {
        var request = HttpRequestDefinition.Create("oauth") with
        {
            Url = "https://api.example.test/me",
            Auth = RequestAuth.None with
            {
                Kind = AuthKind.OAuth2,
                OAuth2 = OAuth2Settings.Default with { TokenUrl = "https://auth.example.test/token", ClientId = "id", ClientSecret = "client-secret-xyz" },
            },
        };
        var vm = CodeGeneration(request, EnvironmentDefinition.Create("dev"));

        foreach (var target in vm.Targets)
        {
            vm.SelectedTarget = target;

            Assert.Contains("Bearer <access token>", vm.Code, StringComparison.Ordinal);
            Assert.DoesNotContain("client-secret-xyz", vm.Code, StringComparison.Ordinal);
        }
    }
}
