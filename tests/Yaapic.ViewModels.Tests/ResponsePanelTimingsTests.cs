using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.ViewModels.Responses;

namespace Yaapic.ViewModels.Tests;

public sealed class ResponsePanelTimingsTests
{
    private static TimeSpan Ms(double v) => TimeSpan.FromMilliseconds(v);

    private static ResponsePanelViewModel Show(RequestTimings? timings, double totalMs = 100, string protocol = "")
    {
        var vm = new ResponsePanelViewModel();
        vm.ShowReport(new SendReport(
            SendResult.Success(new HttpResponseData(200, "OK", [], [], Ms(totalMs), "https://x.test")
            {
                Timings = timings,
                Protocol = protocol,
            }),
            []));
        return vm;
    }

    [Fact]
    public void New_connection_gives_five_rows_with_increasing_offsets()
    {
        var vm = Show(new RequestTimings(Ms(10), Ms(10), Ms(20), Ms(40), Ms(20), false), protocol: "HTTP/2");

        Assert.True(vm.HasTimings);
        Assert.Equal(5, vm.Timings.Count);
        Assert.Equal(
            [Strings.TimingDns, Strings.TimingTcp, Strings.TimingTls, Strings.TimingWaiting, Strings.TimingDownload],
            vm.Timings.Select(r => r.Label));
        Assert.True(vm.Timings.Sum(r => r.Fraction) <= 1.0000001);
        Assert.Equal(0, vm.Timings[0].OffsetFraction);
        for (var i = 1; i < 5; i++)
        {
            Assert.True(vm.Timings[i].OffsetFraction > vm.Timings[i - 1].OffsetFraction);
        }

        Assert.Equal(0.4, vm.Timings[3].Fraction, 6);
        Assert.Equal("HTTP/2", vm.ProtocolText);
        Assert.Equal(Strings.TimingNewConnection, vm.ConnectionText);
        Assert.Equal("100 ms", vm.TotalText);
    }

    [Fact]
    public void Reused_connection_gives_two_rows_and_the_reused_text()
    {
        var vm = Show(new RequestTimings(null, null, null, Ms(60), Ms(40), true));

        Assert.Equal(2, vm.Timings.Count);
        Assert.Equal(Strings.TimingConnectionReused, vm.ConnectionText);
        Assert.All(vm.Timings, r => Assert.True(r.IsMeasured));
    }

    [Fact]
    public void Unmeasured_phases_show_not_available()
    {
        var vm = Show(new RequestTimings(null, Ms(10), null, Ms(50), Ms(40), false));

        var dns = vm.Timings[0];
        Assert.False(dns.IsMeasured);
        Assert.Equal(Strings.TimingNotAvailable, dns.DurationText);
        Assert.Equal(0, dns.Fraction);
        Assert.False(vm.Timings[2].IsMeasured);
        Assert.True(vm.Timings[1].IsMeasured);
    }

    [Fact]
    public void Null_timings_and_unknown_protocol_give_nothing()
    {
        var vm = Show(null);

        Assert.False(vm.HasTimings);
        Assert.Empty(vm.Timings);
        Assert.Equal("", vm.ProtocolText);
        Assert.Equal("", vm.ConnectionText);
    }

    [Fact]
    public void Clear_and_sending_empty_the_rows()
    {
        var vm = Show(new RequestTimings(null, null, null, Ms(60), Ms(40), true), protocol: "HTTP/1.1");

        vm.ShowSending();
        Assert.False(vm.HasTimings);
        Assert.Equal("", vm.ProtocolText);

        vm = Show(new RequestTimings(null, null, null, Ms(60), Ms(40), true));
        vm.Clear();
        Assert.False(vm.HasTimings);
        Assert.Equal("", vm.TotalText);
    }

    [Fact]
    public void Failure_empties_the_rows()
    {
        var vm = Show(new RequestTimings(null, null, null, Ms(60), Ms(40), true));

        vm.ShowReport(new SendReport(SendResult.Fail(SendFailureKind.Unknown, "boom"), []));

        Assert.False(vm.HasTimings);
    }

    [Fact]
    public void Zero_is_invisible_and_tiny_positive_stays_visible()
    {
        var vm = Show(new RequestTimings(null, null, null, Ms(0), Ms(0.001), true), totalMs: 100000);

        Assert.Equal(0, vm.Timings[0].Fraction);
        Assert.Equal(0.005, vm.Timings[1].Fraction);
        Assert.Equal("0 ms", vm.Timings[0].DurationText);
    }

    [Theory]
    [InlineData(12, "12 ms")]
    [InlineData(1340, "1.34 s")]
    public void Duration_text_units(double ms, string expected)
    {
        var vm = Show(new RequestTimings(null, null, null, Ms(ms), Ms(0), true), totalMs: 5000);

        Assert.Equal(expected, vm.Timings[0].DurationText);
    }
}
