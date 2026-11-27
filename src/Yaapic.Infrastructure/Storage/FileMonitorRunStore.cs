using System.Globalization;
using System.Text;
using System.Text.Json;
using Yaapic.Core.Storage;
using Yaapic.Domain.Monitors;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores the monitor run history as JSON lines in <c>.yaapic/monitor-runs.jsonl</c> of a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileMonitorRunStore(string workspaceRoot) : IMonitorRunStore
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly string _path = Path.Combine(workspaceRoot, ".yaapic", "monitor-runs.jsonl");
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc />
    public Task AppendAsync(MonitorRun run, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(run);
        var line = JsonSerializer.Serialize(ToDto(run), MonitorRunJsonContext.Default.MonitorRunLineDto);
        return RunAsync(async () =>
        {
            var lines = await ReadLinesAsync(ct);
            lines.Add(line);
            var excess = lines.Count(l => MonitorIdOf(l) == run.MonitorId) - IMonitorRunStore.MaxRunsPerMonitor;
            if (excess > 0)
            {
                // The oldest lines of this monitor are the first ones in file order.
                var drop = new HashSet<int>(lines
                    .Select((l, i) => (l, i))
                    .Where(x => MonitorIdOf(x.l) == run.MonitorId)
                    .Take(excess)
                    .Select(x => x.i));
                lines = [.. lines.Where((_, i) => !drop.Contains(i))];
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await WorkspaceFiles.WriteAtomicAsync(_path, string.Join('\n', lines) + "\n", ct);
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MonitorRun>> ListAsync(Guid monitorId, int max, CancellationToken ct)
    {
        IReadOnlyList<MonitorRun> result = [];
        await RunAsync(async () =>
        {
            var lines = await ReadLinesAsync(ct);
            result = [.. lines
                .Select((l, i) => (Run: TryParse(l), Index: i))
                .Where(x => x.Run is not null && x.Run.MonitorId == monitorId)
                .OrderByDescending(x => x.Run!.At)
                .ThenByDescending(x => x.Index)
                .Take(Math.Max(0, max))
                .Select(x => x.Run!)];
        });
        return result;
    }

    /// <inheritdoc />
    public async Task<MonitorRun?> LatestAsync(Guid monitorId, CancellationToken ct) =>
        (await ListAsync(monitorId, 1, ct)).FirstOrDefault();

    /// <inheritdoc />
    public Task DeleteAsync(Guid monitorId, CancellationToken ct) =>
        RunAsync(async () =>
        {
            var lines = await ReadLinesAsync(ct);
            var kept = lines.Where(l => MonitorIdOf(l) != monitorId).ToList();
            if (kept.Count != lines.Count)
            {
                await WorkspaceFiles.WriteAtomicAsync(_path, kept.Count == 0 ? "" : string.Join('\n', kept) + "\n", ct);
            }
        });

    private async Task RunAsync(Func<Task> action)
    {
        await _gate.WaitAsync();
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot access monitor runs: {ex.Message}", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Shares delete access: the atomic replace of an append must not fail on Windows because another reader (a second store, a viewer) has the file open.
    private async Task<List<string>> ReadLinesAsync(CancellationToken ct)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, useAsync: true);
        using var reader = new StreamReader(stream, Utf8);
        return [.. (await reader.ReadToEndAsync(ct)).Split('\n').Where(l => l.Trim().Length > 0)];
    }

    private static Guid? MonitorIdOf(string line) => TryParse(line)?.MonitorId;

    private static MonitorRunLineDto ToDto(MonitorRun r) => new()
    {
        MonitorId = r.MonitorId.ToString("D"),
        At = r.At.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        DurationMs = r.DurationMs,
        Status = r.Status.ToString().ToLowerInvariant(),
        Requests = r.Requests,
        FailedRequests = r.FailedRequests,
        TestsPassed = r.TestsPassed,
        TestsFailed = r.TestsFailed,
        Message = r.Message,
        Failures = [.. r.Failures.Select(f => new MonitorFailureDto
        {
            Request = f.RequestName,
            Outcome = f.Outcome,
            Status = f.StatusCode,
            Detail = f.Detail,
        })],
    };

    private static MonitorRun? TryParse(string line)
    {
        try
        {
            var d = JsonSerializer.Deserialize(line, MonitorRunJsonContext.Default.MonitorRunLineDto);
            if (d is null
                || !Guid.TryParseExact(d.MonitorId, "D", out var id)
                || !DateTimeOffset.TryParseExact(d.At, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                || !Enum.TryParse<MonitorRunStatus>(d.Status, true, out var status))
            {
                return null;
            }

            return new MonitorRun(
                id,
                at,
                d.DurationMs,
                status,
                d.Requests,
                d.FailedRequests,
                d.TestsPassed,
                d.TestsFailed,
                d.Message,
                [.. (d.Failures ?? []).Select(f => new MonitorFailure(f.Request ?? "", f.Outcome ?? "", f.Status, f.Detail ?? ""))]);
        }
        catch (Exception ex) when (ex is JsonException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}
