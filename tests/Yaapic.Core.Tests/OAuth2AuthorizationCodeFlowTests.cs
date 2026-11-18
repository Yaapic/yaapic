using System.Text;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class OAuth2AuthorizationCodeFlowTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly InMemoryOAuth2TokenCache _cache = new();
    private readonly FakeLoopbackRedirectReceiver _receiver = new();
    private readonly FakeExternalBrowser _browser = new();

    public OAuth2AuthorizationCodeFlowTests()
    {
        var log = new List<string>();
        _receiver.Log = log;
        _browser.Log = log;
        _receiver.Browser = _browser;
        _receiver.Target = () => $"/callback?code=c1&state={Query("state")}";
    }

    private static OAuth2Settings Settings() => OAuth2Settings.Default with
    {
        Grant = OAuth2Grant.AuthorizationCode,
        AuthorizationUrl = "https://auth/authorize",
        TokenUrl = "https://auth/token",
        ClientId = "id",
        RedirectUri = "http://127.0.0.1:8123/callback",
        Scope = "read",
    };

    private string Query(string name)
    {
        var pair = _browser.Opened[0].Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).First(p => p[0] == name);
        return Uri.UnescapeDataString(pair[1]);
    }

    private (OAuth2TokenService Service, FakeHttpTransport Transport) Create(TimeSpan? timeout = null)
    {
        var body = """{"access_token":"a1","expires_in":3600,"refresh_token":"r1"}""";
        var transport = new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Success(
            new HttpResponseData(200, "", [], Encoding.UTF8.GetBytes(body), TimeSpan.Zero, "https://auth/token"))));
        return (new OAuth2TokenService(transport, _cache, _clock, _receiver, _browser, timeout), transport);
    }

    [Fact]
    public async Task HappyPath_OpensTheUrlExchangesTheCodeAndCaches()
    {
        var (service, transport) = Create();
        var result = await service.GetNewTokenAsync(Settings(), CancellationToken.None);

        Assert.Equal("a1", result.Token!.AccessToken);
        Assert.True(_cache.TryGet(OAuth2TokenKey.For(Settings()), out var cached) && cached.AccessToken == "a1");
        Assert.Equal(8123, _receiver.Port);
        Assert.Equal("/callback", _receiver.Path);
        Assert.Contains("close this tab", _receiver.SuccessHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", _receiver.SuccessHtml, StringComparison.OrdinalIgnoreCase);

        Assert.Equal("code", Query("response_type"));
        Assert.Equal("id", Query("client_id"));
        Assert.Equal("http://127.0.0.1:8123/callback", Query("redirect_uri"));
        Assert.Equal("S256", Query("code_challenge_method"));
        Assert.Equal("read", Query("scope"));

        var body = transport.Requests.Single().BodyText!;
        Assert.Contains("grant_type=authorization_code", body, StringComparison.Ordinal);
        Assert.Contains("code=c1", body, StringComparison.Ordinal);
        var verifier = body.Split('&').First(p => p.StartsWith("code_verifier=", StringComparison.Ordinal))["code_verifier=".Length..];
        Assert.Equal(Query("code_challenge"), Pkce.FromVerifier(verifier).Challenge);
    }

    [Fact]
    public async Task TheBrowserIsOpenedOnlyAfterTheReceiverStartedWaiting()
    {
        var (service, _) = Create();
        await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(["listen", "open"], _receiver.Log);
    }

    [Fact]
    public async Task ErrorRedirect_FailsWithErrorAndDescription()
    {
        _receiver.Target = () => "/callback?error=access_denied&error_description=User+said+no&state=x";
        var (service, transport) = Create();
        var result = await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal("access_denied: User said no", result.Error);
        Assert.Empty(transport.Requests);
    }

    [Theory]
    [InlineData("/callback?code=c1&state=other")]
    [InlineData("/callback?code=c1")]
    public async Task WrongOrMissingState_Fails(string target)
    {
        _receiver.Target = () => target;
        var (service, transport) = Create();
        var result = await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(Strings.Oauth2StateMismatch, result.Error);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task MissingCode_Fails()
    {
        _receiver.Target = () => $"/callback?state={Query("state")}";
        var (service, transport) = Create();
        var result = await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(Strings.Oauth2InvalidResponse(0), result.Error);
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task NoRedirect_TimesOut()
    {
        _receiver.Gate = new TaskCompletionSource().Task;
        var (service, _) = Create(TimeSpan.FromMilliseconds(50));
        var result = await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal(Strings.Oauth2AuthorizationTimeout, result.Error);
    }

    [Fact]
    public async Task CallerCancel_Throws()
    {
        _receiver.Gate = new TaskCompletionSource().Task;
        var (service, _) = Create();
        using var cts = new CancellationTokenSource();
        var pending = service.GetNewTokenAsync(Settings(), cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Theory]
    [InlineData("https://example.com/cb")]
    [InlineData("http://127.0.0.1/cb")]
    [InlineData("")]
    public async Task InvalidRedirectUri_FailsWithoutOpeningTheBrowser(string redirect)
    {
        var (service, _) = Create();
        var result = await service.GetNewTokenAsync(Settings() with { RedirectUri = redirect }, CancellationToken.None);
        Assert.Equal(Strings.Oauth2RedirectUriInvalid, result.Error);
        Assert.Empty(_browser.Opened);
    }

    [Theory]
    [InlineData("", "https://auth/token", "id")]
    [InlineData("https://auth/authorize", "", "id")]
    [InlineData("https://auth/authorize", "https://auth/token", "")]
    public async Task MissingSetting_FailsWithoutOpeningTheBrowser(string authUrl, string tokenUrl, string clientId)
    {
        var (service, _) = Create();
        var result = await service.GetNewTokenAsync(
            Settings() with { AuthorizationUrl = authUrl, TokenUrl = tokenUrl, ClientId = clientId }, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(result.Error));
        Assert.Empty(_browser.Opened);
    }

    [Fact]
    public async Task BusyPort_SurfacesTheMessage()
    {
        _receiver.Failure = new InvalidOperationException("Port 8123 is already in use.");
        var (service, _) = Create();
        var result = await service.GetNewTokenAsync(Settings(), CancellationToken.None);
        Assert.Equal("Port 8123 is already in use.", result.Error);
    }
}
