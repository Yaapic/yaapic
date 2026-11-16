using Yaapic.Core.Auth;
using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class RequestSenderAuthTests
{
    static readonly HttpRequestDefinition Pending = HttpRequestDefinition.Create("r") with
    {
        Url = "http://a/x",
        Auth = RequestAuth.None with { Kind = AuthKind.Digest, Username = "u", Password = "p" },
    };

    static FakeHttpTransport Transport() => new((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "none")));

    static Task<SendReport> Send(RequestSender s, HttpRequestDefinition r) =>
        s.SendAsync(r, (EnvironmentDefinition?)null, CancellationToken.None);

    static ResolvedRequest Sample(string url = "http://a/x", params KeyValueEntry[] headers) =>
        new(HttpMethodKind.Get, url, headers, null, null, []);

    [Fact]
    public async Task Authenticated_request_is_sent_with_added_header()
    {
        var t = Transport();
        var auth = new FakeRequestAuthenticator((r, _) => Task.FromResult(AuthenticationResult.Success(r.WithHeaderUnlessPresent("Authorization", "X"))));
        await Send(new RequestSender(t, auth), Pending);
        Assert.Contains(t.Requests[0].Headers, h => h.Key == "Authorization" && h.Value == "X");
    }

    [Fact]
    public void User_authorization_header_wins()
    {
        var r = Sample(headers: new KeyValueEntry("authorization", "mine")).WithHeaderUnlessPresent("Authorization", "X");
        Assert.Equal([new KeyValueEntry("authorization", "mine")], r.Headers);
    }

    [Fact]
    public void Header_is_appended_after_user_headers()
    {
        var r = Sample(headers: new KeyValueEntry("A", "1")).WithHeaderUnlessPresent("B", "2");
        Assert.Equal(["A", "B"], r.Headers.Select(h => h.Key));
    }

    [Fact]
    public void Query_parameter_goes_after_params_and_before_fragment()
    {
        var r = Sample("http://a/x?q=1#frag").WithQueryParameter("k e", "v&1");
        Assert.Equal("http://a/x?q=1&k%20e=v%261#frag", r.Url);
    }

    [Fact]
    public void Query_parameter_starts_the_query_string() =>
        Assert.Equal("http://a/x?k=v", Sample().WithQueryParameter("k", "v").Url);

    [Fact]
    public async Task Failed_authentication_does_not_call_the_transport()
    {
        var t = Transport();
        var auth = new FakeRequestAuthenticator((_, _) => Task.FromResult(AuthenticationResult.Fail("nope")));
        var report = await Send(new RequestSender(t, auth), Pending);
        Assert.Empty(t.Requests);
        Assert.Equal(SendFailureKind.AuthenticationFailed, report.Result.Failure!.Kind);
        Assert.Equal("nope", report.Result.Failure.Message);
    }

    [Fact]
    public async Task Authenticator_exception_becomes_authentication_failed()
    {
        var t = Transport();
        var auth = new FakeRequestAuthenticator((_, _) => throw new InvalidOperationException("boom"));
        var report = await Send(new RequestSender(t, auth), Pending);
        Assert.Empty(t.Requests);
        Assert.Equal(SendFailureKind.AuthenticationFailed, report.Result.Failure!.Kind);
        Assert.Equal("boom", report.Result.Failure.Message);
    }

    [Fact]
    public async Task No_authenticator_with_pending_auth_fails_cleanly()
    {
        var t = Transport();
        var report = await Send(new RequestSender(t), Pending);
        Assert.Empty(t.Requests);
        Assert.Equal(SendFailureKind.AuthenticationFailed, report.Result.Failure!.Kind);
        Assert.Equal("This authentication type is not available.", report.Result.Failure.Message);
    }

    [Fact]
    public async Task No_pending_auth_never_calls_the_authenticator()
    {
        var auth = new FakeRequestAuthenticator();
        await Send(new RequestSender(Transport(), auth), HttpRequestDefinition.Create("r") with { Url = "http://a" });
        Assert.Empty(auth.Requests);
    }

    [Fact]
    public async Task Cancellation_in_the_authenticator_becomes_canceled()
    {
        var t = Transport();
        var auth = new FakeRequestAuthenticator((_, _) => throw new OperationCanceledException());
        var report = await Send(new RequestSender(t, auth), Pending);
        Assert.Empty(t.Requests);
        Assert.Equal(SendFailureKind.Canceled, report.Result.Failure!.Kind);
    }

    [Fact]
    public async Task Unresolved_variables_are_still_returned_on_failure()
    {
        var report = await Send(new RequestSender(Transport()), Pending with { Url = "http://a/{{nope}}" });
        Assert.Equal(["nope"], report.UnresolvedVariables);
    }
}
