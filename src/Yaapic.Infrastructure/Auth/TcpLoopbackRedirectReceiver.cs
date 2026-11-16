using System.Net;
using System.Net.Sockets;
using System.Text;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Localization;

namespace Yaapic.Infrastructure.Auth;

/// <summary>Captures the OAuth 2.0 redirect with a <see cref="TcpListener"/> bound to the loopback interface only.</summary>
public sealed class TcpLoopbackRedirectReceiver : ILoopbackRedirectReceiver
{
    private const int MaxRequestBytes = 8 * 1024;
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public async Task<string> WaitForRequestTargetAsync(int port, string path, string successHtml, CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
        }
        catch (SocketException)
        {
            throw new InvalidOperationException(Strings.Oauth2PortBusy(port));
        }

        try
        {
            var expected = Normalize(path);
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                var target = await TryHandleAsync(client, port, expected, successHtml, ct).ConfigureAwait(false);
                if (target is not null)
                {
                    return target;
                }
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string Normalize(string path) => path.Length > 1 ? path.TrimEnd('/') : path;

    private static async Task<string?> TryHandleAsync(TcpClient client, int port, string expectedPath, string successHtml, CancellationToken ct)
    {
        var stream = client.GetStream();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ReadTimeout);
        try
        {
            var head = await ReadHeadAsync(stream, timeout.Token).ConfigureAwait(false);
            if (head is null
                || !TryParse(head, out var method, out var target, out var host)
                || (host != $"127.0.0.1:{port}" && host != $"localhost:{port}"))
            {
                await RespondAsync(stream, "400 Bad Request", string.Empty, ct).ConfigureAwait(false);
                return null;
            }

            var queryStart = target.IndexOf('?', StringComparison.Ordinal);
            var requestPath = Normalize(queryStart < 0 ? target : target[..queryStart]);
            if (method != "GET" || requestPath != expectedPath)
            {
                await RespondAsync(stream, "404 Not Found", string.Empty, ct).ConfigureAwait(false);
                return null;
            }

            await RespondAsync(stream, "200 OK", successHtml, ct).ConfigureAwait(false);
            return target;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is OperationCanceledException or IOException or SocketException)
        {
            // A slow or broken client: drop it and keep listening.
            return null;
        }
    }

    private static async Task<string?> ReadHeadAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[MaxRequestBytes];
        var length = 0;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), ct).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            length += read;
            var text = Encoding.ASCII.GetString(buffer, 0, length);
            var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end >= 0)
            {
                return text[..end];
            }
        }

        return null;
    }

    private static bool TryParse(string head, out string method, out string target, out string host)
    {
        method = target = host = string.Empty;
        var lines = head.Split("\r\n");
        var parts = lines[0].Split(' ');
        if (parts.Length != 3 || parts[0].Length == 0 || parts[1].Length == 0 || !parts[2].StartsWith("HTTP/", StringComparison.Ordinal))
        {
            return false;
        }

        method = parts[0];
        target = parts[1];
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && line[..colon].Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                host = line[(colon + 1)..].Trim();
            }
        }

        return true;
    }

    private static async Task RespondAsync(NetworkStream stream, string status, string html, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nConnection: close\r\nContent-Length: {body.Length}\r\n\r\n");
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(body, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
        await DrainAsync(stream, ct).ConfigureAwait(false);
    }

    // Reading what the client still sends avoids a TCP reset that would discard our response (oversize requests).
    private static async Task DrainAsync(NetworkStream stream, CancellationToken ct)
    {
        using var drain = CancellationTokenSource.CreateLinkedTokenSource(ct);
        drain.CancelAfter(TimeSpan.FromMilliseconds(200));
        var buffer = new byte[MaxRequestBytes];
        try
        {
            while (await stream.ReadAsync(buffer, drain.Token).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException)
        {
            // Nothing more to drain.
        }
    }
}
