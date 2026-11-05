using System.Globalization;
using System.Text;
using System.Text.Json;
using Yaapic.Core.Storage;
using Yaapic.Domain.History;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Stores the request history as JSON lines in <c>.yaapic/history.jsonl</c> of a workspace.</summary>
/// <param name="workspaceRoot">The workspace root directory.</param>
public sealed class FileHistoryStore(string workspaceRoot) : IHistoryStore
{
    private const int RewriteThreshold = 550;
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly string _path = Path.Combine(workspaceRoot, ".yaapic", "history.jsonl");
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int? _lineCount;

    /// <inheritdoc />
    public Task AppendAsync(HistoryEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var line = JsonSerializer.Serialize(ToDto(entry), HistoryJsonContext.Default.HistoryLineDto);
        return RunAsync(async () =>
        {
            var count = _lineCount ??= File.Exists(_path) ? (await ReadLinesAsync(ct)).Count : 0;
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var prefix = EndsWithoutNewline() ? "\n" : "";
            await File.AppendAllTextAsync(_path, prefix + line + "\n", Utf8, ct);
            _lineCount = ++count;
            if (count > RewriteThreshold)
            {
                var lines = await ReadLinesAsync(ct);
                await RewriteAsync(lines.Skip(lines.Count - IHistoryStore.MaxEntries), ct);
            }
        });
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoryEntry>> ListAsync(CancellationToken ct)
    {
        IReadOnlyList<HistoryEntry>? result = null;
        await RunAsync(async () =>
        {
            result = [.. (await ReadLinesAsync(ct))
                .Select(l => TryParse(l, out var e) ? e : null)
                .OfType<HistoryEntry>()
                .OrderByDescending(e => e.At)
                .ThenByDescending(e => e.Id)
                .Take(IHistoryStore.MaxEntries)];
        });
        return result!;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken ct) =>
        RunAsync(async () =>
        {
            var lines = await ReadLinesAsync(ct);
            var kept = lines.Where(l => !(TryParse(l, out var e) && e.Id == id)).ToList();
            if (kept.Count != lines.Count)
            {
                await RewriteAsync(kept, ct);
            }
        });

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken ct) =>
        RunAsync(() =>
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            _lineCount = 0;
            return Task.CompletedTask;
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
            _lineCount = null;
            throw new StorageException($"Cannot access history: {ex.Message}", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool EndsWithoutNewline()
    {
        if (!File.Exists(_path))
        {
            return false;
        }

        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (fs.Length == 0)
        {
            return false;
        }

        fs.Seek(-1, SeekOrigin.End);
        return fs.ReadByte() != '\n';
    }

    private async Task<List<string>> ReadLinesAsync(CancellationToken ct) =>
        File.Exists(_path)
            ? [.. (await File.ReadAllTextAsync(_path, Utf8, ct)).Split('\n').Where(l => l.Trim().Length > 0)]
            : [];

    private async Task RewriteAsync(IEnumerable<string> lines, CancellationToken ct)
    {
        var list = lines.ToList();
        await WorkspaceFiles.WriteAtomicAsync(_path, list.Count == 0 ? "" : string.Join('\n', list) + "\n", ct);
        _lineCount = list.Count;
    }

    private static HistoryLineDto ToDto(HistoryEntry e) => new()
    {
        Id = e.Id.ToString("D"),
        At = e.At.ToString("O", CultureInfo.InvariantCulture),
        Request = RequestYamlSerializer.Serialize(e.Request),
        StatusCode = e.StatusCode,
        StatusText = e.StatusText,
        DurationMs = (long)e.Duration.TotalMilliseconds,
        SizeBytes = e.SizeBytes,
        FailureKind = e.FailureKind?.ToString(),
        FailureMessage = e.FailureMessage,
    };

    private static bool TryParse(string line, out HistoryEntry entry)
    {
        entry = null!;
        try
        {
            var d = JsonSerializer.Deserialize(line, HistoryJsonContext.Default.HistoryLineDto);
            if (d is null
                || !Guid.TryParseExact(d.Id, "D", out var id)
                || !DateTimeOffset.TryParseExact(d.At, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                || d.Request is null)
            {
                return false;
            }

            SendFailureKind? kind = null;
            if (d.FailureKind is not null)
            {
                if (!Enum.TryParse<SendFailureKind>(d.FailureKind, out var k))
                {
                    return false;
                }

                kind = k;
            }

            entry = new HistoryEntry(
                id,
                at,
                RequestYamlSerializer.Deserialize(d.Request),
                d.StatusCode,
                d.StatusText ?? "",
                TimeSpan.FromMilliseconds(d.DurationMs),
                d.SizeBytes,
                kind,
                d.FailureMessage);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or StorageException or FormatException or ArgumentException or InvalidDataException)
        {
            return false;
        }
    }
}
