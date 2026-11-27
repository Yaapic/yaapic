using System.Globalization;
using System.Text;
using Yaapic.Core.Mocks;

namespace Yaapic.Infrastructure.Mocks;

/// <summary>Writes HTTP/1.1 responses.</summary>
internal static class Http11ResponseWriter
{
    private static readonly HashSet<string> Ours = new(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Length", "Transfer-Encoding", "Connection", "Date", "Server",
    };

    /// <summary>Writes a mock response.</summary>
    /// <param name="stream">The connection stream.</param>
    /// <param name="response">The response.</param>
    /// <param name="keepAlive">Whether the connection stays open.</param>
    /// <param name="now">The time used for the <c>Date</c> header.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the response is flushed.</returns>
    public static async Task WriteAsync(
        Stream stream, MockHttpResponse response, bool keepAlive, DateTimeOffset now, CancellationToken ct)
    {
        var noBody = response.Status is 204 or 304;
        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 ").Append(response.Status.ToString(CultureInfo.InvariantCulture)).Append(' ')
            .Append(Clean(response.Reason)).Append("\r\n");
        foreach (var h in response.Headers)
        {
            if (h.Enabled && !Ours.Contains(h.Key))
            {
                sb.Append(Clean(h.Key)).Append(": ").Append(Clean(h.Value)).Append("\r\n");
            }
        }

        if (!noBody)
        {
            sb.Append("Content-Length: ").Append(response.Body.Length.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        }

        sb.Append("Date: ").Append(now.UtcDateTime.ToString("R", CultureInfo.InvariantCulture)).Append("\r\n")
            .Append("Server: Yaapic-Mock\r\n")
            .Append(keepAlive ? "Connection: keep-alive" : "Connection: close").Append("\r\n\r\n");
        await stream.WriteAsync(Encoding.Latin1.GetBytes(sb.ToString()), ct);
        if (!noBody && !response.OmitBody)
        {
            await stream.WriteAsync(response.Body, ct);
        }

        await stream.FlushAsync(ct);
    }

    /// <summary>Writes a minimal <c>text/plain</c> error response and announces the close.</summary>
    /// <param name="stream">The connection stream.</param>
    /// <param name="status">The status code.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that completes when the response is flushed.</returns>
    public static Task WriteErrorAsync(Stream stream, int status, CancellationToken ct)
    {
        var reason = status switch
        {
            413 => "Content Too Large",
            431 => "Request Header Fields Too Large",
            505 => "HTTP Version Not Supported",
            _ => ReasonPhrases.For(status),
        };
        var response = new MockHttpResponse(
            status, reason, [new("Content-Type", "text/plain; charset=utf-8")], Encoding.UTF8.GetBytes(reason), TimeSpan.Zero);
        return WriteAsync(stream, response, false, DateTimeOffset.UtcNow, ct);
    }

    private static string Clean(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
}
