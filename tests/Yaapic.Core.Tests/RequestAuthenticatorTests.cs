using System.Text;
using Yaapic.Core.Auth;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class RequestAuthenticatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 8, 0, 0, TimeSpan.Zero);

    private sealed class RecordingMethod(AuthKind kind, string header) : IAuthMethod
    {
        public AuthKind Kind => kind;

        public List<ResolvedRequest> Seen { get; } = [];

        public Task<AuthenticationResult> ApplyAsync(ResolvedRequest request, ResolvedAuth auth, CancellationToken ct)
        {
            Seen.Add(request);
            return Task.FromResult(AuthenticationResult.Success(request.WithHeaderUnlessPresent("X-Auth", header)));
        }
    }

    private static ResolvedRequest Request(AuthKind? kind) =>
        new(HttpMethodKind.Get, "https://example.test/a", [], null, null, [],
            PendingAuth: kind is null ? null : new ResolvedAuth(kind.Value, "", "", null, null, null));

    [Fact]
    public async Task Routes_each_kind_to_its_method()
    {
        var jwt = new RecordingMethod(AuthKind.Jwt, "jwt");
        var digest = new RecordingMethod(AuthKind.Digest, "digest");
        var sut = new RequestAuthenticator([jwt, digest]);

        var result = await sut.AuthenticateAsync(Request(AuthKind.Digest), CancellationToken.None);

        Assert.Equal("digest", result.Request!.Headers.Single().Value);
        Assert.Empty(jwt.Seen);
        Assert.Single(digest.Seen);
    }

    [Fact]
    public async Task Request_without_pending_auth_is_unchanged()
    {
        var method = new RecordingMethod(AuthKind.Jwt, "jwt");
        var request = Request(null);

        var result = await new RequestAuthenticator([method]).AuthenticateAsync(request, CancellationToken.None);

        Assert.Same(request, result.Request);
        Assert.Empty(method.Seen);
    }

    [Fact]
    public async Task Unknown_kind_fails()
    {
        var result = await new RequestAuthenticator([new RecordingMethod(AuthKind.Jwt, "jwt")])
            .AuthenticateAsync(Request(AuthKind.AwsV4), CancellationToken.None);

        Assert.Null(result.Request);
        Assert.Equal("This authentication type is not available.", result.ErrorMessage);
    }

    [Fact]
    public void Duplicate_kinds_are_rejected() =>
        Assert.Throws<ArgumentException>(() => new RequestAuthenticator([new RecordingMethod(AuthKind.Jwt, "a"), new RecordingMethod(AuthKind.Jwt, "b")]));

    [Fact]
    public async Task Pending_auth_is_cleared_before_the_method_runs()
    {
        var method = new RecordingMethod(AuthKind.Jwt, "jwt");

        var result = await new RequestAuthenticator([method]).AuthenticateAsync(Request(AuthKind.Jwt), CancellationToken.None);

        Assert.Null(Assert.Single(method.Seen).PendingAuth);
        Assert.Null(result.Request!.PendingAuth);
    }

    private static HttpRequestDefinition Definition(RequestAuth auth) =>
        HttpRequestDefinition.Create("r") with { Url = "https://example.test/api", Auth = auth };

    private static SendResult Reply(int status, string body = "", params KeyValueEntry[] headers) =>
        SendResult.Success(new HttpResponseData(status, "", headers, Encoding.UTF8.GetBytes(body), TimeSpan.Zero, "https://example.test/api"));

    private static async Task<(FakeHttpTransport Transport, SendReport Report)> Send(
        RequestAuth auth, Func<ResolvedRequest, SendResult> reply)
    {
        var transport = new FakeHttpTransport((r, _) => Task.FromResult(reply(r)));
        var clock = new FakeTimeProvider(Now);
        var tokens = new OAuth2TokenService(transport, new InMemoryOAuth2TokenCache(), clock, new FakeLoopbackRedirectReceiver(), new FakeExternalBrowser());
        var sender = new RequestSender(transport, RequestAuthenticator.CreateDefault(transport, tokens, clock));
        return (transport, await sender.SendAsync(Definition(auth), (EnvironmentDefinition?)null, CancellationToken.None));
    }

    [Fact]
    public async Task Sender_sends_a_jwt_bearer_header()
    {
        var auth = RequestAuth.None with
        {
            Kind = AuthKind.Jwt,
            Jwt = JwtSettings.Default with { Secret = "s3cret", Payload = "{\"sub\":\"me\"}" },
        };

        var (transport, _) = await Send(auth, _ => Reply(200));

        var header = Assert.Single(transport.Requests).Headers.Single(h => h.Key == "Authorization").Value;
        Assert.StartsWith("Bearer ", header);
        Assert.Equal(3, header[7..].Split('.').Length);
    }

    [Fact]
    public async Task Sender_signs_with_aws_v4_at_the_fixed_time()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.AwsV4, AwsV4 = new("AKID", "secret", "", "us-east-1", "s3") };

        var (transport, _) = await Send(auth, _ => Reply(200));

        var headers = Assert.Single(transport.Requests).Headers;
        Assert.Equal("20261112T080000Z", headers.Single(h => h.Key == "x-amz-date").Value);
        Assert.StartsWith("AWS4-HMAC-SHA256 Credential=AKID/20261112/us-east-1/s3/aws4_request", headers.Single(h => h.Key == "Authorization").Value);
    }

    [Fact]
    public async Task Sender_fetches_an_oauth2_token_then_calls_the_api()
    {
        var auth = RequestAuth.None with
        {
            Kind = AuthKind.OAuth2,
            OAuth2 = OAuth2Settings.Default with { TokenUrl = "https://example.test/token", ClientId = "id", ClientSecret = "s" },
        };

        var (transport, report) = await Send(auth, r =>
            r.Url.EndsWith("/token", StringComparison.Ordinal) ? Reply(200, """{"access_token":"tok","token_type":"Bearer","expires_in":3600}""") : Reply(200));

        Assert.Equal(2, transport.Requests.Count);
        Assert.Equal("https://example.test/token", transport.Requests[0].Url);
        Assert.Equal("Bearer tok", transport.Requests[1].Headers.Single(h => h.Key == "Authorization").Value);
        Assert.Equal(200, report.Result.Response!.StatusCode);
    }

    [Fact]
    public async Task Sender_answers_a_digest_challenge()
    {
        var auth = RequestAuth.None with { Kind = AuthKind.Digest, Username = "Mufasa", Password = "Circle Of Life" };
        var challenge = new KeyValueEntry("WWW-Authenticate", "Digest realm=\"r\", qop=\"auth\", nonce=\"abc\"");

        var (transport, _) = await Send(auth, r =>
            r.Headers.Any(h => h.Key == "Authorization") ? Reply(200) : Reply(401, "", challenge));

        Assert.Equal(2, transport.Requests.Count);
        Assert.StartsWith("Digest username=\"Mufasa\"", transport.Requests[1].Headers.Single(h => h.Key == "Authorization").Value);
    }
}
