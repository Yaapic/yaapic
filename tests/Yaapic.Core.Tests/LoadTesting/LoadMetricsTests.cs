using Yaapic.Core.LoadTesting;

namespace Yaapic.Core.Tests.LoadTesting;

public class LoadMetricsTests
{
    private static readonly TimeSpan Ten = TimeSpan.FromSeconds(10);

    private static LoadSample S(int duration, long at = 0, int request = 0, bool error = false)
        => new(at, request, duration, error ? 500 : 200, error);

    [Fact]
    public void Empty_metrics_give_empty_stats()
    {
        var m = new LoadMetrics(1);
        Assert.Equal(LoadStats.Empty, m.Overall(Ten));
        Assert.Empty(m.Snapshot(Ten).Buckets);
    }

    [Fact]
    public void Single_sample_fills_every_percentile()
    {
        var m = new LoadMetrics(1);
        m.Add(S(42));
        var s = m.Overall(TimeSpan.FromSeconds(2));
        Assert.Equal((42, 42, 42, 42, 42, 42), (s.MinMs, s.MaxMs, s.P50Ms, s.P90Ms, s.P95Ms, s.P99Ms));
        Assert.Equal(0.5, s.Throughput);
    }

    [Fact]
    public void Hundred_samples_use_nearest_rank()
    {
        var m = new LoadMetrics(1);
        for (var i = 100; i >= 1; i--)
        {
            m.Add(S(i));
        }

        var s = m.Overall(Ten);
        Assert.Equal(100, s.Count);
        Assert.Equal((1, 100, 50.5), (s.MinMs, s.MaxMs, s.AverageMs));
        Assert.Equal((50, 90, 95, 99), (s.P50Ms, s.P90Ms, s.P95Ms, s.P99Ms));
        Assert.Equal(10, s.Throughput);
    }

    [Fact]
    public void Zero_elapsed_gives_zero_throughput()
    {
        var m = new LoadMetrics(1);
        m.Add(S(5));
        Assert.Equal(0, m.Overall(TimeSpan.Zero).Throughput);
    }

    [Fact]
    public void Error_rate_and_per_request_stats()
    {
        var m = new LoadMetrics(2);
        m.Add(S(10, request: 0));
        m.Add(S(30, request: 0, error: true));
        m.Add(S(100, request: 1));
        m.Add(S(200, request: 1));
        Assert.Equal(0.25, m.Overall(Ten).ErrorRate);
        var first = m.ForRequest(0, Ten);
        Assert.Equal((2, 1, 0.5, 20), (first.Count, first.Errors, first.ErrorRate, first.AverageMs));
        var second = m.ForRequest(1, Ten);
        Assert.Equal((100, 200, 100), (second.MinMs, second.MaxMs, second.P50Ms));
    }

    [Fact]
    public void Buckets_fill_gaps_and_carry_active_users()
    {
        var m = new LoadMetrics(1);
        m.SetActiveUsers(0, 2);
        m.SetActiveUsers(500, 3);
        m.Add(S(10, at: 100));
        m.Add(S(30, at: 900, error: true));
        m.Add(S(50, at: 3200));
        m.SetActiveUsers(3500, 5);
        var snap = m.Snapshot(TimeSpan.FromSeconds(4));
        Assert.Equal(
            [new LoadBucket(0, 2, 1, 20, 3), new LoadBucket(1, 0, 0, 0, 3), new LoadBucket(2, 0, 0, 0, 3), new LoadBucket(3, 1, 0, 50, 5)],
            snap.Buckets);
        Assert.Equal(5, snap.ActiveUsers);
        Assert.Equal(1, snap.LastSecondThroughput);
        Assert.Equal(2, m.Snapshot(TimeSpan.FromSeconds(1.5)).LastSecondThroughput);
    }

    [Fact]
    public void Truncation_keeps_counts_exact()
    {
        var m = new LoadMetrics(1, maxStoredSamples: 3);
        for (var i = 1; i <= 10; i++)
        {
            m.Add(S(i));
        }

        var s = m.Overall(Ten);
        Assert.True(m.SamplesTruncated);
        Assert.Equal((10, 1, 10, 5.5), (s.Count, s.MinMs, s.MaxMs, s.AverageMs));
        Assert.Equal(2, s.P50Ms);
    }

    [Fact]
    public void Keeps_the_first_twenty_distinct_error_messages()
    {
        var m = new LoadMetrics(1);
        for (var i = 0; i < 30; i++)
        {
            m.AddError($"e{i}");
            m.AddError("e0");
        }

        Assert.Equal(20, m.ErrorMessages.Count);
        Assert.Equal("e0", m.ErrorMessages[0]);
        Assert.Equal("e19", m.ErrorMessages[19]);
    }

    [Fact]
    public async Task Concurrent_adds_are_exact()
    {
        var m = new LoadMetrics(1);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 10_000; i++)
            {
                m.Add(S(i % 50 + 1, at: i % 3000, error: i % 10 == 0));
            }
        })));
        var s = m.Overall(Ten);
        Assert.Equal((80_000, 8_000), (s.Count, s.Errors));
        Assert.Equal(80_000, m.Snapshot(Ten).Buckets.Sum(b => b.Requests));
    }
}
