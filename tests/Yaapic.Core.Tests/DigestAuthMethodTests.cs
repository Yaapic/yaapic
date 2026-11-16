using Yaapic.Core.Auth;
using Yaapic.Core.Auth.Digest;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class DigestAuthMethodTests
{
    private const string Challenge =
        "Digest realm=\"testrealm@host.com\", qop=\"auth,auth-int\", nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"";

    private static ResolvedRequest Request(HttpMethodKind method = HttpMethodKind.Get, string? body = null, params KeyValueEntry[] headers) =>
        new(method, "https://example.test/dir/index.html?x=1", [new KeyValueEntry("Accept", "*/*"), .. headers], body, body is null ? null : "text/plain", []);

    private static ResolvedAuth Auth(string user = "Mufasa", string password = "Circle Of Life") =>
        new(AuthKind.Digest, user, password, null, null, null);

    private static SendResult Reply(int status, params KeyValueEntry[] headers) =>
        SendResult.Success(new HttpResponseData(status, "", headers, [], TimeSpan.Zero, "https://example.test/dir/index.html?x=1"));

    private static FakeHttpTransport Transport(SendResult result) => new((_, _) => Task.FromResult(result));

    private static Task<AuthenticationResult> Run(FakeHttpTransport t, ResolvedRequest request, ResolvedAuth? auth = null) =>
        new DigestAuthMethod(t, () => "0a4f113b").ApplyAsync(request, auth ?? Auth(), CancellationToken.None);

    [Fact]
    public async Task Challenge_yields_the_expected_header_and_probe_is_bare()
    {
        var t = Transport(Reply(401, new KeyValueEntry("www-authenticate", Challenge)));
        var request = Request(HttpMethodKind.Post, "payload");
        var result = await Run(t, request with { PendingAuth = Auth() });

        var probe = Assert.Single(t.Requests);
        Assert.Equal(HttpMethodKind.Post, probe.Method);
        Assert.Equal(request.Url, probe.Url);
        Assert.Equal(request.Headers, probe.Headers);
        Assert.Null(probe.BodyText);
        Assert.Null(probe.BodyContentType);
        Assert.Null(probe.PendingAuth);

        var header = result.Request!.Headers.Single(h => h.Key == "Authorization").Value;
        Assert.StartsWith("Digest username=\"Mufasa\", realm=\"testrealm@host.com\"", header);
        Assert.Contains("uri=\"/dir/index.html?x=1\"", header);
        Assert.Contains("cnonce=\"0a4f113b\"", header);
        Assert.Equal("payload", result.Request.BodyText);
    }

    [Fact]
    public async Task Non_401_probe_leaves_the_request_unchanged()
    {
        var request = Request();
        var result = await Run(Transport(Reply(200)), request);

        Assert.Equal(request, result.Request);
    }

    [Fact]
    public async Task Basic_only_challenge_leaves_the_request_unchanged()
    {
        var request = Request();
        var result = await Run(Transport(Reply(401, new KeyValueEntry("WWW-Authenticate", "Basic realm=\"x\""))), request);

        Assert.Equal(request, result.Request);
    }

    [Fact]
    public async Task Several_headers_are_joined_and_the_digest_one_is_found()
    {
        var t = Transport(Reply(401, new KeyValueEntry("WWW-Authenticate", "Basic realm=\"x\""), new KeyValueEntry("WWW-Authenticate", Challenge)));
        var result = await Run(t, Request());

        Assert.Contains(result.Request!.Headers, h => h.Key == "Authorization");
    }

    [Fact]
    public async Task Transport_failure_is_reported()
    {
        var result = await Run(Transport(SendResult.Fail(SendFailureKind.Unknown, "boom")), Request());

        Assert.Null(result.Request);
        Assert.Equal("boom", result.ErrorMessage);
    }

    [Fact]
    public async Task Missing_credentials_fail_without_probing()
    {
        var t = Transport(Reply(200));
        var result = await Run(t, Request(), Auth("", ""));

        Assert.Null(result.Request);
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(t.Requests);
    }

    [Fact]
    public async Task User_authorization_header_wins_and_is_not_probed()
    {
        var t = Transport(Reply(401, new KeyValueEntry("WWW-Authenticate", Challenge)));
        var request = Request(headers: new KeyValueEntry("authorization", "Bearer x"));
        var result = await Run(t, request);

        Assert.DoesNotContain(t.Requests.Single().Headers, h => h.Key == "authorization");
        Assert.Equal(request, result.Request);
    }

    [Fact]
    public async Task Unsupported_qop_fails_with_the_message()
    {
        var t = Transport(Reply(401, new KeyValueEntry("WWW-Authenticate", "Digest realm=\"r\", nonce=\"n\", qop=\"auth-int\"")));
        var result = await Run(t, Request());

        Assert.Null(result.Request);
        Assert.Contains("auth-int", result.ErrorMessage);
    }

    [Fact]
    public async Task Default_constructor_uses_a_random_hex_nonce()
    {
        var t = Transport(Reply(401, new KeyValueEntry("WWW-Authenticate", Challenge)));
        var result = await new DigestAuthMethod(t).ApplyAsync(Request(), Auth(), CancellationToken.None);

        Assert.Matches("cnonce=\"[0-9a-f]{32}\"", result.Request!.Headers.Single(h => h.Key == "Authorization").Value);
    }
}
