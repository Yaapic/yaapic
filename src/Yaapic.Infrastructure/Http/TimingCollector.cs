using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Http;

/// <summary>
/// Collects the timestamps of one request and turns them into <see cref="RequestTimings"/>.
/// Redirects share the request message, hence the collector: every new connection overwrites the connection phases, so the
/// phases reported are those of the <b>last</b> connection opened (the last hop that needed one) while <c>Wait</c> and <c>Download</c>
/// cover the final response. One request, one collector; writes are locked because connection callbacks run on pool threads.
/// </summary>
internal sealed class TimingCollector
{
    /// <summary>The key under which the collector travels in <see cref="HttpRequestMessage.Options"/>.</summary>
    internal static readonly HttpRequestOptionsKey<TimingCollector> Key = new("yaapic.timings");

    private readonly object _gate = new();
    private long _start;
    private long _dnsStart;
    private long _dnsEnd;
    private long _connectEnd;
    private long _tlsEnd;
    private long _headers;
    private long _bodyEnd;
    private bool _connectionOpened;

    /// <summary>Marks the moment the request is handed to the client.</summary>
    internal void Start() => Set(ref _start);

    /// <summary>Marks the arrival of the response headers.</summary>
    internal void HeadersReceived() => Set(ref _headers);

    /// <summary>Marks the end of the body download.</summary>
    internal void BodyEnded() => Set(ref _bodyEnd);

    private void Set(ref long field)
    {
        var now = Stopwatch.GetTimestamp();
        lock (_gate)
            field = now;
    }

    /// <summary>Records a new connection: DNS start, DNS end and TCP connect end; resets the TLS marker.</summary>
    internal void ConnectionOpened(long dnsStart, long dnsEnd, long connectEnd)
    {
        lock (_gate)
        {
            _dnsStart = dnsStart;
            _dnsEnd = dnsEnd;
            _connectEnd = connectEnd;
            _tlsEnd = 0;
            _connectionOpened = true;
        }
    }

    /// <summary>Records the end of the TLS handshake of the connection just opened.</summary>
    internal void TlsEnded() => Set(ref _tlsEnd);

    /// <summary>Builds the timings; phases not measured stay <see langword="null"/>.</summary>
    internal RequestTimings Build()
    {
        lock (_gate)
        {
            var connectionEnd = _tlsEnd != 0 ? _tlsEnd : _connectEnd;
            var waitFrom = _connectionOpened ? connectionEnd : _start;
            return new RequestTimings(
                _connectionOpened ? Elapsed(_dnsStart, _dnsEnd) : null,
                _connectionOpened ? Elapsed(_dnsEnd, _connectEnd) : null,
                _connectionOpened && _tlsEnd != 0 ? Elapsed(_connectEnd, _tlsEnd) : null,
                Elapsed(waitFrom, _headers),
                Elapsed(_headers, _bodyEnd),
                !_connectionOpened);
        }
    }

    private static TimeSpan Elapsed(long from, long to) => to > from ? Stopwatch.GetElapsedTime(from, to) : TimeSpan.Zero;

    /// <summary>Connects like the handler would, timing DNS and TCP connect for the request's collector.</summary>
    internal static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var dnsStart = Stopwatch.GetTimestamp();
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        var dnsEnd = Stopwatch.GetTimestamp();
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
        }
        catch
        {
            socket.Dispose();
            throw;
        }

        if (context.InitialRequestMessage?.Options.TryGetValue(Key, out var collector) == true)
            collector.ConnectionOpened(dnsStart, dnsEnd, Stopwatch.GetTimestamp());
        return new NetworkStream(socket, ownsSocket: true);
    }

    /// <summary>Marks the end of the TLS handshake (the stream is an <see cref="SslStream"/> only for https).</summary>
    internal static ValueTask<Stream> MarkTlsAsync(SocketsHttpPlaintextStreamFilterContext context, CancellationToken ct)
    {
        if (context.PlaintextStream is SslStream
            && context.InitialRequestMessage.Options.TryGetValue(Key, out var collector))
            collector.TlsEnded();
        return ValueTask.FromResult(context.PlaintextStream);
    }
}
