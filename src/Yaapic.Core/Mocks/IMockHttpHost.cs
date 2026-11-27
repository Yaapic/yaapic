using Yaapic.Localization;

namespace Yaapic.Core.Mocks;

/// <summary>Listens on the loopback interface and serves HTTP requests to a handler.</summary>
public interface IMockHttpHost
{
    /// <summary>Starts listening on <c>127.0.0.1:{port}</c>.</summary>
    /// <param name="port">The port to listen on (not 0).</param>
    /// <param name="handler">Answers each request.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The running server; dispose it to stop listening.</returns>
    /// <exception cref="MockPortInUseException">The port is already in use or not allowed.</exception>
    Task<IMockHttpServer> StartAsync(
        int port, Func<MockHttpRequest, CancellationToken, Task<MockHttpResponse>> handler, CancellationToken ct);
}

/// <summary>A running mock HTTP server.</summary>
public interface IMockHttpServer : IAsyncDisposable
{
    /// <summary>Gets the port the server listens on.</summary>
    int Port { get; }
}

/// <summary>The requested port is already in use.</summary>
/// <param name="port">The port.</param>
public sealed class MockPortInUseException(int port) : Exception(Strings.MockPortInUse(port))
{
    /// <summary>Gets the port.</summary>
    public int Port { get; } = port;
}
