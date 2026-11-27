using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Yaapic.Core.Mocks;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Mocks;

/// <summary>Serves mock servers with a <see cref="TcpListener"/> bound to the loopback interface only.</summary>
/// <param name="time">The clock used for delays and the <c>Date</c> header.</param>
public sealed class TcpMockHttpHost(TimeProvider time) : IMockHttpHost
{
    internal const int MaxConnections = 100;

    private int _accepted;

    internal TimeProvider Time { get; } = time;

    /// <summary>Gets the number of connections accepted so far (test hook).</summary>
    internal int AcceptedConnections => Volatile.Read(ref _accepted);

    /// <inheritdoc />
    public Task<IMockHttpServer> StartAsync(
        int port, Func<MockHttpRequest, CancellationToken, Task<MockHttpResponse>> handler, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        if (OperatingSystem.IsWindows())
        {
            listener.ExclusiveAddressUse = true;
        }

        try
        {
            listener.Start();
        }
        catch (SocketException e) when (e.SocketErrorCode is SocketError.AddressAlreadyInUse or SocketError.AccessDenied)
        {
            throw new MockPortInUseException(port);
        }

        return Task.FromResult<IMockHttpServer>(new Server(this, listener, port, handler));
    }

    private sealed class Server : IMockHttpServer
    {
        private readonly TcpMockHttpHost _host;
        private readonly TcpListener _listener;
        private readonly Func<MockHttpRequest, CancellationToken, Task<MockHttpResponse>> _handler;
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentDictionary<int, Task> _connections = new();
        private readonly Task _acceptLoop;
        private int _open;
        private int _nextId;

        public Server(
            TcpMockHttpHost host, TcpListener listener, int port,
            Func<MockHttpRequest, CancellationToken, Task<MockHttpResponse>> handler)
        {
            _host = host;
            _listener = listener;
            Port = port;
            _handler = handler;
            _acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public int Port { get; }

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();
            _listener.Stop();
            var pending = _connections.Values.Append(_acceptLoop).ToArray();
            try
            {
                await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception e) when (e is TimeoutException or OperationCanceledException)
            {
                // Give up on stragglers: their sockets are closed with the cancellation.
            }

            _cts.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            var ct = _cts.Token;
            while (!ct.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(ct);
                }
                catch (Exception e) when (e is OperationCanceledException or ObjectDisposedException or SocketException)
                {
                    return;
                }

                Interlocked.Increment(ref _host._accepted);
                var id = Interlocked.Increment(ref _nextId);
                var overLimit = Interlocked.Increment(ref _open) > MaxConnections;
                _connections[id] = Task.Run(async () =>
                {
                    try
                    {
                        using (client)
                        {
                            await ServeAsync(client.GetStream(), overLimit, ct);
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _open);
                        _connections.TryRemove(id, out _);
                    }
                });
            }
        }

        private async Task ServeAsync(Stream stream, bool overLimit, CancellationToken ct)
        {
            try
            {
                if (overLimit)
                {
                    await Http11ResponseWriter.WriteErrorAsync(stream, 503, ct);
                    return;
                }

                using var reader = new Http11RequestReader(stream, Http11Limits.Default);
                while (true)
                {
                    var result = await reader.ReadAsync(ct);
                    if (result is Http11ReadResult.Error error)
                    {
                        await Http11ResponseWriter.WriteErrorAsync(stream, error.Status, ct);
                        return;
                    }

                    if (result is not Http11ReadResult.Request request)
                    {
                        return;
                    }

                    if (!await AnswerAsync(stream, request, ct))
                    {
                        return;
                    }
                }
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or SocketException or ObjectDisposedException)
            {
                // Cancelled by disposal or dropped by the client.
            }
        }

        // Returns whether the connection can serve another request.
        private async Task<bool> AnswerAsync(Stream stream, Http11ReadResult.Request request, CancellationToken ct)
        {
            MockHttpResponse response;
            try
            {
                response = await _handler(request.Value, ct);
                if (response.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(response.Delay, _host.Time, ct);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                response = new MockHttpResponse(
                    500, "Internal Server Error", [new KeyValueEntry("Content-Type", "text/plain; charset=utf-8")],
                    Encoding.UTF8.GetBytes("Mock server error"), TimeSpan.Zero);
            }

            await Http11ResponseWriter.WriteAsync(stream, response, request.KeepAlive, _host.Time.GetUtcNow(), ct);
            return request.KeepAlive;
        }
    }
}
