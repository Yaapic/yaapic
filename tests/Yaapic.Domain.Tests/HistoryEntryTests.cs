using Yaapic.Domain.History;
using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class HistoryEntryTests
{
    static readonly DateTimeOffset At = new(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2));

    static HttpRequestDefinition Req() => HttpRequestDefinition.Create("a") with { Url = "{{host}}/x" };

    [Fact]
    public void From_success_copies_response()
    {
        var req = Req();
        var resp = new HttpResponseData(404, "Not Found", [], [1, 2, 3], TimeSpan.FromMilliseconds(12), "http://x");
        var e = HistoryEntry.From(req, SendResult.Success(resp), At);
        Assert.True(e.IsSuccess);
        Assert.Equal(404, e.StatusCode);
        Assert.Equal("Not Found", e.StatusText);
        Assert.Equal(TimeSpan.FromMilliseconds(12), e.Duration);
        Assert.Equal(3, e.SizeBytes);
        Assert.Null(e.FailureKind);
        Assert.Null(e.FailureMessage);
        Assert.Same(req, e.Request);
        Assert.NotEqual(Guid.Empty, e.Id);
    }

    [Theory]
    [InlineData(SendFailureKind.InvalidUrl)]
    [InlineData(SendFailureKind.Timeout)]
    [InlineData(SendFailureKind.ConnectionFailed)]
    [InlineData(SendFailureKind.Canceled)]
    [InlineData(SendFailureKind.FileUnreadable)]
    [InlineData(SendFailureKind.Unknown)]
    public void From_failure_keeps_kind_and_message(SendFailureKind kind)
    {
        var e = HistoryEntry.From(Req(), SendResult.Fail(kind, "boom"), At);
        Assert.False(e.IsSuccess);
        Assert.Null(e.StatusCode);
        Assert.Equal("", e.StatusText);
        Assert.Equal(TimeSpan.Zero, e.Duration);
        Assert.Equal(0, e.SizeBytes);
        Assert.Equal(kind, e.FailureKind);
        Assert.Equal("boom", e.FailureMessage);
    }

    [Fact]
    public void From_converts_to_utc()
    {
        var e = HistoryEntry.From(Req(), SendResult.Fail(SendFailureKind.Unknown, "x"), At);
        Assert.Equal(TimeSpan.Zero, e.At.Offset);
        Assert.Equal(At, e.At);
    }

    [Fact]
    public void IsEquivalentTo_compares_every_member()
    {
        var e = HistoryEntry.From(Req(), SendResult.Fail(SendFailureKind.Timeout, "t"), At);
        Assert.True(e.IsEquivalentTo(e with { Request = e.Request with { } }));
        Assert.False(e.IsEquivalentTo(e with { Id = Guid.NewGuid() }));
        Assert.False(e.IsEquivalentTo(e with { At = At.AddSeconds(1) }));
        Assert.False(e.IsEquivalentTo(e with { Request = e.Request with { Url = "other" } }));
        Assert.False(e.IsEquivalentTo(e with { StatusCode = 200 }));
        Assert.False(e.IsEquivalentTo(e with { StatusText = "OK" }));
        Assert.False(e.IsEquivalentTo(e with { Duration = TimeSpan.FromSeconds(1) }));
        Assert.False(e.IsEquivalentTo(e with { SizeBytes = 1 }));
        Assert.False(e.IsEquivalentTo(e with { FailureKind = SendFailureKind.Unknown }));
        Assert.False(e.IsEquivalentTo(e with { FailureMessage = "z" }));
    }
}
