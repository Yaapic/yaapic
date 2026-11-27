using Yaapic.Core.Mocks;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IMockHttpHost"/> that listens on nothing: tests call the handler of a started port directly.</summary>
public sealed class FakeMockHttpHost : IMockHttpHost
{
    private readonly Dictionary<int, Func<MockHttpRequest, CancellationToken, Task<MockHttpResponse>>> _handlers = [];

    /// <summary>Gets the ports started so far, in order (stopped ones included).</summary>
    public List<int> StartedPorts { get; } = [];

    /// <summary>Gets the ports that are told to be in use.</summary>
    public HashSet<int> PortsInUse { get; } = [];

    /// <summary>Gets the ports currently listening.</summary>
    public IReadOnlyCollection<int> ListeningPorts => _handlers.Keys;

    /// <inheritdoc/>
    public Task<IMockHttpServer> StartAsync(
        int port, Func<MockHttpRequest, CancellationToken, Task<MockHttpResponse>> handler, CancellationToken ct)
    {
        if (PortsInUse.Contains(port) || _handlers.ContainsKey(port))
        {
            throw new MockPortInUseException(port);
        }

        _handlers[port] = handler;
        StartedPorts.Add(port);
        return Task.FromResult<IMockHttpServer>(new Server(this, port));
    }

    /// <summary>Sends a request to the handler of a listening port.</summary>
    /// <param name="port">The port.</param>
    /// <param name="request">The request.</param>
    /// <returns>The handler's response.</returns>
    public Task<MockHttpResponse> SendAsync(int port, MockHttpRequest request) =>
        _handlers[port](request, CancellationToken.None);

    private sealed class Server(FakeMockHttpHost owner, int port) : IMockHttpServer
    {
        public int Port => port;

        public ValueTask DisposeAsync()
        {
            owner._handlers.Remove(port);
            return ValueTask.CompletedTask;
        }
    }
}
