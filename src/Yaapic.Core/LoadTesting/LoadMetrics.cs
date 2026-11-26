namespace Yaapic.Core.LoadTesting;

/// <summary>Thread-safe collector that turns load samples into statistics, buckets and snapshots.</summary>
public sealed class LoadMetrics
{
    private const int MaxErrorMessages = 20;

    private readonly object _gate = new();
    private readonly int _maxStoredSamples;
    private readonly Totals _overall = new();
    private readonly Totals[] _perRequest;
    private readonly List<int> _overallDurations = [];
    private readonly List<int>[] _requestDurations;
    private readonly List<Bucket> _buckets = [];
    private readonly List<string> _errorMessages = [];
    private long _stored;
    private int _activeUsers;
    private bool _truncated;

    /// <summary>Initializes a new instance of the <see cref="LoadMetrics"/> class.</summary>
    /// <param name="requestCount">Number of requests in the plan.</param>
    /// <param name="maxStoredSamples">Maximum number of durations kept for percentiles.</param>
    public LoadMetrics(int requestCount, int maxStoredSamples = 2_000_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requestCount);
        ArgumentOutOfRangeException.ThrowIfNegative(maxStoredSamples);
        _maxStoredSamples = maxStoredSamples;
        _perRequest = new Totals[requestCount];
        _requestDurations = new List<int>[requestCount];
        for (var i = 0; i < requestCount; i++)
        {
            _perRequest[i] = new Totals();
            _requestDurations[i] = [];
        }
    }

    /// <summary>Gets the first distinct error messages (at most 20).</summary>
    public IReadOnlyList<string> ErrorMessages
    {
        get
        {
            lock (_gate)
            {
                return [.. _errorMessages];
            }
        }
    }

    /// <summary>Gets a value indicating whether some durations were not stored for percentiles.</summary>
    public bool SamplesTruncated
    {
        get
        {
            lock (_gate)
            {
                return _truncated;
            }
        }
    }

    /// <summary>Records one sample.</summary>
    /// <param name="sample">The sample.</param>
    public void Add(LoadSample sample)
    {
        lock (_gate)
        {
            var known = (uint)sample.RequestIndex < (uint)_perRequest.Length;
            _overall.Add(sample);
            if (known)
            {
                _perRequest[sample.RequestIndex].Add(sample);
            }

            var bucket = BucketAt(sample.ElapsedMs);
            bucket.Requests++;
            bucket.SumMs += sample.DurationMs;
            if (sample.IsError)
            {
                bucket.Errors++;
            }

            if (_stored >= _maxStoredSamples)
            {
                _truncated = true;
                return;
            }

            _stored++;
            _overallDurations.Add(sample.DurationMs);
            if (known)
            {
                _requestDurations[sample.RequestIndex].Add(sample.DurationMs);
            }
        }
    }

    /// <summary>Records an error message, keeping the first 20 distinct ones.</summary>
    /// <param name="message">The message.</param>
    public void AddError(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            if (_errorMessages.Count < MaxErrorMessages && !_errorMessages.Contains(message, StringComparer.Ordinal))
            {
                _errorMessages.Add(message);
            }
        }
    }

    /// <summary>Records the active user count at a point in time.</summary>
    /// <param name="elapsedMs">Milliseconds since the start.</param>
    /// <param name="users">Active virtual users.</param>
    public void SetActiveUsers(long elapsedMs, int users)
    {
        lock (_gate)
        {
            BucketAt(elapsedMs).Users = users;
            _activeUsers = users;
        }
    }

    /// <summary>Builds a snapshot of the current state.</summary>
    /// <param name="elapsed">Time since the start.</param>
    /// <returns>The snapshot.</returns>
    public LoadSnapshot Snapshot(TimeSpan elapsed)
    {
        lock (_gate)
        {
            var buckets = new List<LoadBucket>(_buckets.Count);
            var users = 0;
            for (var second = 0; second < _buckets.Count; second++)
            {
                var b = _buckets[second];
                users = b.Users ?? users;
                buckets.Add(new LoadBucket(second, b.Requests, b.Errors, b.Requests == 0 ? 0 : (double)b.SumMs / b.Requests, users));
            }

            var lastComplete = (int)Math.Floor(elapsed.TotalSeconds) - 1;
            var lastSecond = lastComplete >= 0 && lastComplete < buckets.Count ? buckets[lastComplete].Requests : 0;
            return new LoadSnapshot(elapsed, _activeUsers, Compute(_overall, _overallDurations, elapsed), lastSecond, buckets);
        }
    }

    /// <summary>Computes the statistics over all samples.</summary>
    /// <param name="elapsed">Time since the start, used for throughput.</param>
    /// <returns>The statistics.</returns>
    public LoadStats Overall(TimeSpan elapsed)
    {
        lock (_gate)
        {
            return Compute(_overall, _overallDurations, elapsed);
        }
    }

    /// <summary>Computes the statistics of one request.</summary>
    /// <param name="index">Zero-based request index.</param>
    /// <param name="elapsed">Time since the start, used for throughput.</param>
    /// <returns>The statistics.</returns>
    public LoadStats ForRequest(int index, TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _perRequest.Length);
        lock (_gate)
        {
            return Compute(_perRequest[index], _requestDurations[index], elapsed);
        }
    }

    private static LoadStats Compute(Totals totals, List<int> durations, TimeSpan elapsed)
    {
        if (totals.Count == 0)
        {
            return LoadStats.Empty;
        }

        var sorted = durations.ToArray();
        Array.Sort(sorted);
        var seconds = elapsed.TotalSeconds;
        return new LoadStats(
            totals.Count,
            totals.Errors,
            (double)totals.Errors / totals.Count,
            totals.Min,
            (double)totals.Sum / totals.Count,
            totals.Max,
            Percentile(sorted, 50),
            Percentile(sorted, 90),
            Percentile(sorted, 95),
            Percentile(sorted, 99),
            seconds > 0 ? totals.Count / seconds : 0);
    }

    private static int Percentile(int[] sorted, int percent)
    {
        if (sorted.Length == 0)
        {
            return 0;
        }

        var rank = (int)Math.Ceiling(percent / 100d * sorted.Length);
        return sorted[Math.Clamp(rank, 1, sorted.Length) - 1];
    }

    private Bucket BucketAt(long elapsedMs)
    {
        var second = (int)Math.Max(0, elapsedMs / 1000);
        while (_buckets.Count <= second)
        {
            _buckets.Add(new Bucket());
        }

        return _buckets[second];
    }

    private sealed class Totals
    {
        public long Count;
        public long Errors;
        public long Sum;
        public int Min = int.MaxValue;
        public int Max = int.MinValue;

        public void Add(LoadSample sample)
        {
            Count++;
            Sum += sample.DurationMs;
            Min = Math.Min(Min, sample.DurationMs);
            Max = Math.Max(Max, sample.DurationMs);
            if (sample.IsError)
            {
                Errors++;
            }
        }
    }

    private sealed class Bucket
    {
        public int Requests;
        public int Errors;
        public long SumMs;
        public int? Users;
    }
}
