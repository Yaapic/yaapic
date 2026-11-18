using System.Text.Json;
using Yaapic.Core.Auth;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Auth;
using Yaapic.Infrastructure.Http;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

public sealed class Milestone4AuthenticationEndToEndTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly ClockStub _clock = new(Start);
    private readonly AuthStubServer _server;
    private readonly HttpClient _client = DefaultHttpClient.Create();
    private readonly HttpClient _noRedirect = DefaultHttpClient.CreateNonRedirecting();
    private readonly HttpClientTransport _transport;
    private readonly HttpClient _browserClient = new();
    private readonly OAuth2TokenService _tokens;
    private readonly RequestSender _sender;

    public Milestone4AuthenticationEndToEndTests()
    {
        _server = new AuthStubServer(Start);
        _transport = new HttpClientTransport(_client, _noRedirect, TimeSpan.FromSeconds(10));
        _tokens = new OAuth2TokenService(
            _transport, new InMemoryOAuth2TokenCache(), _clock, new TcpLoopbackRedirectReceiver(), new GetBrowser(_browserClient));
        _sender = new RequestSender(_transport, RequestAuthenticator.CreateDefault(_transport, _tokens, _clock));
    }

    public void Dispose()
    {
        _browserClient.Dispose();
        _transport.Dispose();
        _client.Dispose();
        _noRedirect.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task Client_credentials_token_is_requested_once_reused_and_fetched_again_after_expiry()
    {
        _server.ExpiresIn = 60;
        _server.IssueRefreshToken = false;
        var request = Request("/api", OAuth(OAuth2Grant.ClientCredentials));

        Assert.Equal(200, await StatusAsync(request));
        Assert.Equal(200, await StatusAsync(request));
        Assert.Equal(["client_credentials"], _server.Grants);

        _clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal(200, await StatusAsync(request));
        Assert.Equal(["client_credentials", "client_credentials"], _server.Grants);
    }

    [Fact]
    public async Task Client_credentials_with_the_credentials_in_the_body_are_accepted()
    {
        var settings = OAuth(OAuth2Grant.ClientCredentials) with { ClientAuthentication = OAuth2ClientAuthentication.RequestBody };

        Assert.Equal(200, await StatusAsync(Request("/api", settings)));
    }

    [Fact]
    public async Task Expired_token_is_renewed_with_the_refresh_token()
    {
        _server.ExpiresIn = 60;
        var request = Request("/api", OAuth(OAuth2Grant.Password));

        Assert.Equal(200, await StatusAsync(request));
        _clock.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(200, await StatusAsync(request));

        Assert.Equal(["password", "refresh_token"], _server.Grants);
    }

    [Fact]
    public async Task Password_grant_sends_the_access_token()
    {
        Assert.Equal(200, await StatusAsync(Request("/api", OAuth(OAuth2Grant.Password))));
        Assert.Equal(["password"], _server.Grants);
    }

    [Fact]
    public async Task Authorization_code_with_PKCE_goes_through_the_real_loopback_receiver()
    {
        var settings = OAuth(OAuth2Grant.AuthorizationCode) with
        {
            AuthorizationUrl = _server.BaseUrl + "/authorize",
            RedirectUri = $"http://127.0.0.1:{FreePort()}/callback",
        };

        var token = await _tokens.GetNewTokenAsync(settings, TestContext.Current.CancellationToken);

        Assert.NotNull(token.Token);
        Assert.True(_server.PkceVerified);
        Assert.Equal(["authorization_code"], _server.Grants);
        Assert.Equal(200, await StatusAsync(Request("/api", settings)));
        Assert.Single(_server.Grants);
    }

    [Fact]
    public async Task Wrong_client_secret_fails_with_the_server_description_and_leaks_no_secret()
    {
        var settings = OAuth(OAuth2Grant.ClientCredentials) with { ClientSecret = "wrong-secret-value" };

        var report = await _sender.SendAsync(Request("/api", settings), Scopes(), TestContext.Current.CancellationToken);

        var failure = report.Result.Failure;
        Assert.Equal(SendFailureKind.AuthenticationFailed, failure?.Kind);
        Assert.Contains("Client authentication failed", failure!.Message);
        Assert.DoesNotContain("wrong-secret-value", failure.Message);
        Assert.DoesNotContain(AuthStubServer.ClientSecret, failure.Message);
        Assert.DoesNotContain("t1", failure.Message);
    }

    [Fact]
    public async Task Digest_authentication_answers_the_challenge()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.Digest, Username = AuthStubServer.Username, Password = AuthStubServer.Password };

        Assert.Equal(200, await StatusAsync(Request("/digest?x=1", auth)));
    }

    [Fact]
    public async Task Digest_with_a_wrong_password_passes_the_401_through_as_a_normal_response()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.Digest, Username = AuthStubServer.Username, Password = "not-the-password" };

        var report = await _sender.SendAsync(Request("/digest", auth), Scopes(), TestContext.Current.CancellationToken);

        Assert.Equal(401, report.Result.Response?.StatusCode);
    }

    [Fact]
    public async Task Jwt_HS256_claims_are_signed_with_the_configured_issue_time_and_expiry()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = Jwt(AuthStubServer.JwtSecret, """{"sub":"alice"}""") };

        var report = await _sender.SendAsync(Request("/jwt", auth), Scopes(), TestContext.Current.CancellationToken);

        Assert.Equal(200, report.Result.Response?.StatusCode);
        using var claims = JsonDocument.Parse(report.Result.Response!.Body);
        var iat = Start.ToUnixTimeSeconds();
        Assert.Equal("alice", claims.RootElement.GetProperty("sub").GetString());
        Assert.Equal(iat, claims.RootElement.GetProperty("iat").GetInt64());
        Assert.Equal(iat + 300, claims.RootElement.GetProperty("exp").GetInt64());
    }

    [Fact]
    public async Task Jwt_signed_with_another_secret_is_rejected_by_the_server()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = Jwt("another-secret", "{}") };

        Assert.Equal(401, await StatusAsync(Request("/jwt", auth)));
    }

    [Fact]
    public async Task Jwt_with_an_invalid_payload_fails_without_leaking_the_secret()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.Jwt, Jwt = Jwt(AuthStubServer.JwtSecret, "not json") };

        var report = await _sender.SendAsync(Request("/jwt", auth), Scopes(), TestContext.Current.CancellationToken);

        Assert.Equal(SendFailureKind.AuthenticationFailed, report.Result.Failure?.Kind);
        Assert.DoesNotContain(AuthStubServer.JwtSecret, report.Result.Failure!.Message);
    }

    [Fact]
    public async Task Aws_signature_is_accepted_and_a_tampered_secret_is_denied()
    {
        Assert.Equal(200, await StatusAsync(Request("/aws?b=2&a=1", Aws(AuthStubServer.AwsSecret))));
        Assert.Equal(403, await StatusAsync(Request("/aws?b=2&a=1", Aws("tampered-secret"))));
    }

    [Fact]
    public async Task User_set_Authorization_header_is_kept_and_no_token_request_is_made()
    {
        var request = Request("/api", OAuth(OAuth2Grant.ClientCredentials)) with
        {
            Headers = [new KeyValueEntry("Authorization", "Bearer t1")],
        };

        Assert.Equal(200, await StatusAsync(request));
        Assert.Empty(_server.Grants);
    }

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        probe.Start();
        var port = ((System.Net.IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static Yaapic.Core.Variables.VariableScopes Scopes() => new(null, [], []);

    private HttpRequestDefinition Request(string path, RequestAuth auth) =>
        HttpRequestDefinition.Create("auth") with { Url = _server.BaseUrl + path, Auth = auth };

    private HttpRequestDefinition Request(string path, OAuth2Settings settings) =>
        Request(path, RequestAuth.None with { Kind = AuthKind.OAuth2, OAuth2 = settings });

    private async Task<int> StatusAsync(HttpRequestDefinition request)
    {
        var report = await _sender.SendAsync(request, Scopes(), TestContext.Current.CancellationToken);
        return report.Result.Response?.StatusCode ?? throw new InvalidOperationException(report.Result.Failure?.Message);
    }

    private OAuth2Settings OAuth(OAuth2Grant grant) => OAuth2Settings.Default with
    {
        Grant = grant,
        TokenUrl = _server.BaseUrl + "/token",
        ClientId = AuthStubServer.ClientId,
        ClientSecret = AuthStubServer.ClientSecret,
        Username = AuthStubServer.Username,
        Password = AuthStubServer.Password,
    };

    private static JwtSettings Jwt(string secret, string payload) =>
        JwtSettings.Default with { Secret = secret, Payload = payload, ExpiresInSeconds = 300 };

    private static RequestAuth Aws(string secret) => RequestAuth.None with
    {
        Kind = AuthKind.AwsV4,
        AwsV4 = new AwsV4Settings(AuthStubServer.AwsAccessKey, secret, "", AuthStubServer.AwsRegion, AuthStubServer.AwsService),
    };

    private sealed class ClockStub(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>Opens the authorization URL with a plain GET, following the redirect to the loopback receiver.</summary>
    private sealed class GetBrowser(HttpClient client) : IExternalBrowser
    {
        public async Task OpenAsync(Uri url, CancellationToken ct) => (await client.GetAsync(url, ct)).Dispose();
    }
}
