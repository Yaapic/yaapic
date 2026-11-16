using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class RequestTimingsTests
{
    static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

    [Fact]
    public void Sum_adds_every_phase() =>
        Assert.Equal(Ms(15), new RequestTimings(Ms(1), Ms(2), Ms(3), Ms(4), Ms(5), false).Sum);

    [Fact]
    public void Sum_ignores_null_phases() =>
        Assert.Equal(Ms(9), new RequestTimings(null, null, null, Ms(4), Ms(5), true).Sum);

    [Fact]
    public void Empty_is_all_zero()
    {
        var e = RequestTimings.Empty;
        Assert.Null(e.Dns);
        Assert.Null(e.Connect);
        Assert.Null(e.Tls);
        Assert.Equal(TimeSpan.Zero, e.Wait);
        Assert.Equal(TimeSpan.Zero, e.Download);
        Assert.False(e.ConnectionReused);
        Assert.Equal(TimeSpan.Zero, e.Sum);
    }
}
