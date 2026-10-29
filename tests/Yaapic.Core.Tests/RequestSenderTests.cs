using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;

namespace Yaapic.Core.Tests;

public class RequestSenderTests
{
    static FakeHttpTransport FakeTransport(Func<SendResult>? reply = null) =>
        new((_, _) => Task.FromResult(reply?.Invoke() ?? SendResult.Fail(SendFailureKind.Unknown, "none")));

    static EnvironmentDefinition Env(params (string K, string V)[] vars) =>
        EnvironmentDefinition.Create("e") with { Variables = [.. vars.Select(v => new KeyValueEntry(v.K, v.V))] };

    static HttpRequestDefinition Req(string url) => HttpRequestDefinition.Create("r") with { Url = url };

    [Fact]
    public async Task Substitutes_environment_variable_into_url()
    {
        var t = FakeTransport();
        await new RequestSender(t).SendAsync(Req("{{host}}/x"), Env(("host", "http://a.com")), CancellationToken.None);
        Assert.Equal("http://a.com/x", t.Requests[0].Url);
    }

    [Fact]
    public async Task Substitutes_dynamic_variable()
    {
        var t = FakeTransport();
        await new RequestSender(t).SendAsync(Req("http://a/{{$guid}}"), null, CancellationToken.None);
        Assert.True(Guid.TryParse(t.Requests[0].Url["http://a/".Length..], out _));
    }

    [Fact]
    public async Task Environment_wins_over_dynamic_name()
    {
        var t = FakeTransport();
        await new RequestSender(t).SendAsync(Req("http://a/{{$guid}}"), Env(("$guid", "fixed")), CancellationToken.None);
        Assert.Equal("http://a/fixed", t.Requests[0].Url);
    }

    [Fact]
    public async Task Surfaces_unresolved_names_and_still_sends()
    {
        var t = FakeTransport();
        var report = await new RequestSender(t).SendAsync(Req("http://a/{{nope}}"), null, CancellationToken.None);
        Assert.Equal(["nope"], report.UnresolvedVariables);
        Assert.Single(t.Requests);
    }

    [Fact]
    public async Task Passes_transport_result_through()
    {
        var expected = SendResult.Fail(SendFailureKind.Timeout, "slow");
        var report = await new RequestSender(FakeTransport(() => expected)).SendAsync(Req("http://a"), null, CancellationToken.None);
        Assert.Same(expected, report.Result);
    }

    [Fact]
    public async Task Transport_exception_becomes_unknown()
    {
        var report = await new RequestSender(FakeTransport(() => throw new InvalidOperationException("boom")))
            .SendAsync(Req("http://a"), null, CancellationToken.None);
        Assert.Equal(SendFailureKind.Unknown, report.Result.Failure!.Kind);
        Assert.Equal("boom", report.Result.Failure.Message);
    }

    [Fact]
    public async Task Operation_canceled_becomes_canceled()
    {
        var report = await new RequestSender(FakeTransport(() => throw new OperationCanceledException()))
            .SendAsync(Req("http://a"), null, CancellationToken.None);
        Assert.Equal(SendFailureKind.Canceled, report.Result.Failure!.Kind);
        Assert.Equal("Request canceled", report.Result.Failure.Message);
    }
}
