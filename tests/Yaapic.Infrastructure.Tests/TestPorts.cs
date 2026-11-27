using System.Net;
using System.Net.Sockets;

namespace Yaapic.Infrastructure.Tests;

/// <summary>
/// Hands out loopback ports to the tests that run side by side. Probing port 0 and closing the probe lets the operating system give the
/// same port to another test or to an outgoing connection before the server binds it, so the ports come from a range below the ephemeral
/// one, each used once per process.
/// </summary>
internal static class TestPorts
{
    private const int First = 20_000;
    private const int Count = 10_000;
    private static int _next = Random.Shared.Next(Count);

    /// <summary>Gets a port that nothing listens on and that no other test of this process was given.</summary>
    /// <returns>The port.</returns>
    public static int Next()
    {
        while (true)
        {
            var port = First + (int)((uint)Interlocked.Increment(ref _next) % Count);
            try
            {
                var probe = new TcpListener(IPAddress.Loopback, port);
                probe.Start();
                probe.Stop();
                return port;
            }
            catch (SocketException)
            {
                // Taken or reserved by the system: try the next one.
            }
        }
    }
}
