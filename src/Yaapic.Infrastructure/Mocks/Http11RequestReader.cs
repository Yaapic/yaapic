using System.Buffers;
using System.Globalization;
using System.Text;
using Yaapic.Core.Mocks;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Mocks;

/// <summary>Reads HTTP/1.1 requests from a stream, keeping read-ahead bytes for pipelined requests.</summary>
internal sealed class Http11RequestReader : IDisposable
{
    private static readonly byte[] Continue = "HTTP/1.1 100 Continue\r\n\r\n"u8.ToArray();

    private readonly Stream _stream;
    private readonly Http11Limits _limits;
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(8192);
    private int _start;
    private int _end;

    /// <summary>Initialises a reader.</summary>
    /// <param name="stream">The connection stream.</param>
    /// <param name="limits">The limits to enforce.</param>
    public Http11RequestReader(Stream stream, Http11Limits limits)
    {
        _stream = stream;
        _limits = limits;
    }

    private sealed class RefusedException(int status) : Exception
    {
        public int Status { get; } = status;
    }

    /// <summary>Reads the next request.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The request, a clean close or an error status.</returns>
    public async Task<Http11ReadResult> ReadAsync(CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_limits.ReadTimeout);
        try
        {
            return await ReadCoreAsync(timeout.Token);
        }
        catch (RefusedException e)
        {
            return new Http11ReadResult.Error(e.Status);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new Http11ReadResult.Closed();
        }
        catch (IOException)
        {
            return new Http11ReadResult.Closed();
        }
    }

    /// <summary>Returns the pooled buffer.</summary>
    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_buffer);
        _buffer = [];
    }

    private async Task<Http11ReadResult> ReadCoreAsync(CancellationToken ct)
    {
        var headBytes = 0;
        var requestLine = await ReadLineAsync(ct, headBytes);
        if (requestLine is null)
        {
            return _end == _start ? new Http11ReadResult.Closed() : new Http11ReadResult.Error(400);
        }

        headBytes += requestLine.Length + 2;
        var parts = requestLine.Split(' ');
        if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || !parts[2].StartsWith("HTTP/", StringComparison.Ordinal))
        {
            return new Http11ReadResult.Error(400);
        }

        var version = parts[2];
        if (version is not ("HTTP/1.0" or "HTTP/1.1"))
        {
            return new Http11ReadResult.Error(505);
        }

        var headers = new List<KeyValueEntry>();
        while (true)
        {
            var line = await ReadLineAsync(ct, headBytes) ?? throw new RefusedException(400);
            headBytes += line.Length + 2;
            if (line.Length == 0)
            {
                break;
            }

            if (line[0] is ' ' or '\t')
            {
                throw new RefusedException(400);
            }

            var colon = line.IndexOf(':');
            if (colon <= 0 || line[..colon].Any(c => c is ' ' or '\t'))
            {
                throw new RefusedException(400);
            }

            if (headers.Count >= _limits.MaxHeaderCount)
            {
                throw new RefusedException(431);
            }

            headers.Add(new KeyValueEntry(line[..colon], line[(colon + 1)..].Trim()));
        }

        var body = await ReadBodyAsync(headers, version, ct);
        var request = MockHttpRequest.Parse(parts[0], parts[1], headers, body, version);
        var connection = Tokens(headers, "Connection");
        var keepAlive = version == "HTTP/1.1"
            ? !connection.Contains("close", StringComparer.OrdinalIgnoreCase)
            : connection.Contains("keep-alive", StringComparer.OrdinalIgnoreCase);
        return new Http11ReadResult.Request(request, keepAlive);
    }

    private static List<string> Tokens(List<KeyValueEntry> headers, string name) =>
        headers.Where(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            .SelectMany(h => h.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToList();

    private async Task<byte[]> ReadBodyAsync(List<KeyValueEntry> headers, string version, CancellationToken ct)
    {
        var codings = Tokens(headers, "Transfer-Encoding");
        var lengths = headers.Where(h => h.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.Value).Distinct().ToList();
        if (codings.Count > 0 && lengths.Count > 0)
        {
            throw new RefusedException(400);
        }

        if (codings.Count > 0)
        {
            if (codings.Count != 1 || !codings[0].Equals("chunked", StringComparison.OrdinalIgnoreCase))
            {
                throw new RefusedException(501);
            }

            await ExpectContinueAsync(headers, version, ct);
            return await ReadChunkedAsync(ct);
        }

        if (lengths.Count == 0)
        {
            return [];
        }

        if (lengths.Count > 1 || !long.TryParse(lengths[0], NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            throw new RefusedException(400);
        }

        if (length > _limits.MaxBodyBytes)
        {
            throw new RefusedException(413);
        }

        if (length == 0)
        {
            return [];
        }

        await ExpectContinueAsync(headers, version, ct);
        var body = new byte[length];
        await ReadExactAsync(body, ct);
        return body;
    }

    private async Task ExpectContinueAsync(List<KeyValueEntry> headers, string version, CancellationToken ct)
    {
        if (version == "HTTP/1.1" && Tokens(headers, "Expect").Contains("100-continue", StringComparer.OrdinalIgnoreCase))
        {
            await _stream.WriteAsync(Continue, ct);
            await _stream.FlushAsync(ct);
        }
    }

    private async Task<byte[]> ReadChunkedAsync(CancellationToken ct)
    {
        using var body = new MemoryStream();
        while (true)
        {
            var sizeLine = await ReadLineAsync(ct, 0) ?? throw new RefusedException(400);
            var semicolon = sizeLine.IndexOf(';');
            var hex = (semicolon < 0 ? sizeLine : sizeLine[..semicolon]).Trim();
            if (hex.Length == 0 || hex.Length > 8 || !int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var size) || size < 0)
            {
                throw new RefusedException(400);
            }

            if (size == 0)
            {
                break;
            }

            if (body.Length + size > _limits.MaxBodyBytes)
            {
                throw new RefusedException(413);
            }

            var chunk = new byte[size];
            await ReadExactAsync(chunk, ct);
            body.Write(chunk);
            if (await ReadLineAsync(ct, 0) is not { Length: 0 })
            {
                throw new RefusedException(400);
            }
        }

        var trailerBytes = 0;
        while (true)
        {
            var trailer = await ReadLineAsync(ct, trailerBytes) ?? throw new RefusedException(400);
            if (trailer.Length == 0)
            {
                return body.ToArray();
            }

            trailerBytes += trailer.Length + 2;
        }
    }

    // Returns null at end of stream without a complete line. Throws 431 once the head would exceed the limit.
    private async Task<string?> ReadLineAsync(CancellationToken ct, int alreadyRead)
    {
        var scanned = 0;
        while (true)
        {
            var span = _buffer.AsSpan(_start, _end - _start);
            var nl = span[scanned..].IndexOf((byte)'\n');
            if (nl >= 0)
            {
                var lineLength = scanned + nl;
                var content = span[..lineLength];
                if (content.Length > 0 && content[^1] == '\r')
                {
                    content = content[..^1];
                }

                var line = Encoding.Latin1.GetString(content);
                _start += lineLength + 1;
                return line;
            }

            scanned = span.Length;
            if (alreadyRead + scanned > _limits.MaxHeaderBytes)
            {
                throw new RefusedException(431);
            }

            if (!await FillAsync(ct))
            {
                return null;
            }
        }
    }

    private async Task ReadExactAsync(byte[] target, CancellationToken ct)
    {
        var filled = 0;
        while (filled < target.Length)
        {
            if (_start == _end && !await FillAsync(ct))
            {
                throw new RefusedException(400);
            }

            var n = Math.Min(target.Length - filled, _end - _start);
            _buffer.AsSpan(_start, n).CopyTo(target.AsSpan(filled));
            _start += n;
            filled += n;
        }
    }

    // Reads more bytes into the buffer, compacting or growing it; false at end of stream.
    private async Task<bool> FillAsync(CancellationToken ct)
    {
        if (_start == _end)
        {
            _start = _end = 0;
        }
        else if (_end == _buffer.Length)
        {
            if (_start > 0)
            {
                Buffer.BlockCopy(_buffer, _start, _buffer, 0, _end - _start);
                _end -= _start;
                _start = 0;
            }
            else
            {
                var bigger = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
                Buffer.BlockCopy(_buffer, 0, bigger, 0, _end);
                ArrayPool<byte>.Shared.Return(_buffer);
                _buffer = bigger;
            }
        }

        var read = await _stream.ReadAsync(_buffer.AsMemory(_end), ct);
        _end += read;
        return read > 0;
    }
}
