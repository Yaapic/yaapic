using System.Text;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class OAuth2TokenServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly InMemoryOAuth2TokenCache _cache = new();
    private readonly List<string> _bodies = [];
    private Func<string, SendResult> _reply = _ => Json(200, """{"access_token":"a1","expires_in":3600,"refresh_token":"r1"}""");

    private static OAuth2Settings Settings() =>
        OAuth2Settings.Default with { TokenUrl = "https://t/token", ClientId = "id", ClientSecret = "s" };

    private static SendResult Json(int status, string body) =>
        SendResult.Success(new HttpResponseData(status, "", [], Encoding.UTF8.GetBytes(body), TimeSpan.Zero, "https://t/token"));

    private (OAuth2TokenService Service, FakeHttpTransport Transport) Create()
    {
        var transport = new FakeHttpTransport((r, _) =>
        {
            lock (_bodies)
            {
                _bodies.Add(r.BodyText ?? "");
            }

            return Task.FromResult(_reply(r.BodyText ?? ""));
        });
        return (new OAuth2TokenService(transport, _cache, _clock, new FakeLoopbackRedirectReceiver(), new FakeExternalBrowser()), transport);
    }

    [Fact]
    public async Task TokensChanged_IsRaisedOnSetRemoveAndClear()
    {
        var (service, _) = Create();
        var count = 0;
        service.TokensChanged += (_, _) => count++;

        await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(1, count);

        service.Clear(Settings());
        Assert.Equal(2, count);

        await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        _cache.Clear();
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task GetToken_FetchesThenUsesTheCache()
    {
        var (service, transport) = Create();
        var first = await service.GetTokenAsync(Settings(), CancellationToken.None);
        var second = await service.GetTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal("a1", first.Token!.AccessToken);
        Assert.Equal("a1", second.Token!.AccessToken);
        Assert.Single(transport.Requests);
        Assert.StartsWith("grant_type=client_credentials", _bodies[0]);
    }

    [Fact]
    public async Task GetToken_PasswordGrant_UsesThePasswordRequest()
    {
        var (service, _) = Create();
        await service.GetTokenAsync(Settings() with { Grant = OAuth2Grant.Password, Username = "u", Password = "p" }, CancellationToken.None);
        Assert.StartsWith("grant_type=password&username=u&password=p", _bodies[0]);
    }

    [Fact]
    public async Task GetToken_AfterExpiryMinusLeeway_Refreshes()
    {
        var (service, transport) = Create();
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(3600 - 29));
        _reply = _ => Json(200, """{"access_token":"a2","expires_in":3600}""");
        var result = await service.GetTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(2, transport.Requests.Count);
        Assert.StartsWith("grant_type=refresh_token&refresh_token=r1", _bodies[1]);
        Assert.Equal("a2", result.Token!.AccessToken);
        Assert.Equal("r1", result.Token.RefreshToken); // kept: the answer had none
        Assert.Equal("r1", _cache.TryGet(OAuth2TokenKey.For(Settings()), out var cached) ? cached.RefreshToken : null);
    }

    [Fact]
    public async Task GetToken_JustBeforeLeeway_StillUsesTheCache()
    {
        var (service, transport) = Create();
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(3600 - 31));
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task GetToken_FailedRefresh_FallsBackToAFreshFetch()
    {
        var (service, transport) = Create();
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(2));
        _reply = body => body.StartsWith("grant_type=refresh_token", StringComparison.Ordinal)
            ? Json(400, """{"error":"invalid_grant"}""")
            : Json(200, """{"access_token":"a3","expires_in":60}""");
        var result = await service.GetTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(3, transport.Requests.Count);
        Assert.StartsWith("grant_type=client_credentials", _bodies[2]);
        Assert.Equal("a3", result.Token!.AccessToken);
        Assert.Null(result.Token.RefreshToken);
    }

    [Theory]
    [InlineData("", OAuth2Grant.ClientCredentials, "id", "u")]
    [InlineData("not a url", OAuth2Grant.ClientCredentials, "id", "u")]
    [InlineData("ftp://t/token", OAuth2Grant.ClientCredentials, "id", "u")]
    [InlineData("https://t/token", OAuth2Grant.ClientCredentials, "", "u")]
    [InlineData("https://t/token", OAuth2Grant.Password, "id", "")]
    public async Task GetToken_MissingField_FailsWithoutSending(string url, OAuth2Grant grant, string clientId, string user)
    {
        var (service, transport) = Create();
        var s = Settings() with { TokenUrl = url, Grant = grant, ClientId = clientId, Username = user };
        var result = await service.GetTokenAsync(s, CancellationToken.None);
        Assert.Null(result.Token);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task GetToken_AuthorizationCodeWithoutToken_AsksToGetOneFirst()
    {
        var (service, transport) = Create();
        var result = await service.GetTokenAsync(Settings() with { Grant = OAuth2Grant.AuthorizationCode }, CancellationToken.None);
        Assert.Equal(Yaapic.Localization.Strings.Oauth2GetTokenFirst, result.Error);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task GetToken_AuthorizationCodeWithRefreshToken_Refreshes()
    {
        var s = Settings() with { Grant = OAuth2Grant.AuthorizationCode };
        _cache.Set(OAuth2TokenKey.For(s), new OAuth2Token("old", "Bearer", "r9", null, Start.AddSeconds(10), Start));
        var (service, _) = Create();
        var result = await service.GetTokenAsync(s, CancellationToken.None);
        Assert.Equal("a1", result.Token!.AccessToken);
        Assert.StartsWith("grant_type=refresh_token&refresh_token=r9", _bodies[0]);
    }

    [Fact]
    public async Task GetToken_ConcurrentCallsShareOneRequest()
    {
        var gate = new TaskCompletionSource<SendResult>();
        var transport = new FakeHttpTransport((_, _) => gate.Task);
        var service = new OAuth2TokenService(transport, _cache, _clock, new FakeLoopbackRedirectReceiver(), new FakeExternalBrowser());
        var a = service.GetTokenAsync(Settings(), CancellationToken.None);
        var b = service.GetTokenAsync(Settings(), CancellationToken.None);
        gate.SetResult(Json(200, """{"access_token":"a1"}"""));
        Assert.Equal("a1", (await a).Token!.AccessToken);
        Assert.Equal("a1", (await b).Token!.AccessToken);
        Assert.Single(transport.Requests);
    }

    [Fact]
    public async Task GetToken_CancelledCaller_Throws()
    {
        var gate = new TaskCompletionSource<SendResult>();
        var service = new OAuth2TokenService(new FakeHttpTransport((_, _) => gate.Task), _cache, _clock, new FakeLoopbackRedirectReceiver(), new FakeExternalBrowser());
        using var cts = new CancellationTokenSource();
        var pending = service.GetTokenAsync(Settings(), cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        gate.SetResult(Json(200, """{"access_token":"a1"}"""));
    }

    [Fact]
    public async Task GetToken_TransportFailure_SurfacesItsMessage()
    {
        _reply = _ => SendResult.Fail(SendFailureKind.ConnectionFailed, "connection refused");
        var (service, _) = Create();
        var result = await service.GetTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal("connection refused", result.Error);
    }

    [Fact]
    public async Task GetToken_ErrorJson_SurfacesErrorAndDescription()
    {
        _reply = _ => Json(400, """{"error":"invalid_client","error_description":"bad secret"}""");
        var (service, _) = Create();
        var result = await service.GetTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal("invalid_client: bad secret", result.Error);
        Assert.False(_cache.TryGet(OAuth2TokenKey.For(Settings()), out _));
    }

    [Fact]
    public async Task GetNewToken_IgnoresTheCache()
    {
        var (service, transport) = Create();
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        _reply = _ => Json(200, """{"access_token":"a9"}""");
        var result = await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal("a9", result.Token!.AccessToken);
        Assert.True(_cache.TryGet(OAuth2TokenKey.For(Settings()), out var cached) && cached.AccessToken == "a9");
    }

    [Fact]
    public async Task Clear_RemovesTheEntry()
    {
        var (service, _) = Create();
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        service.Clear(Settings());
        Assert.False(_cache.TryGet(OAuth2TokenKey.For(Settings()), out _));
    }

    [Fact]
    public async Task ClearAllTokens_EmptiesTheCache()
    {
        var (service, _) = Create();
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        service.ClearAllTokens();
        Assert.False(_cache.TryGet(OAuth2TokenKey.For(Settings()), out _));
    }

    [Fact]
    public async Task GetStatus_ReportsNoTokenValidAndExpired()
    {
        var (service, _) = Create();
        Assert.Equal(OAuth2TokenState.NoToken, service.GetStatus(Settings()).State);
        await service.GetTokenAsync(Settings(), CancellationToken.None);
        var valid = service.GetStatus(Settings());
        Assert.Equal(OAuth2TokenState.Valid, valid.State);
        Assert.Equal(Start.AddSeconds(3600), valid.ExpiresAt);
        _clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(OAuth2TokenState.Expired, service.GetStatus(Settings()).State);
    }
}
