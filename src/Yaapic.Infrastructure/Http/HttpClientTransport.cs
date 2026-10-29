using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Http;

/// <summary>An <see cref="IHttpTransport"/> built on <see cref="HttpClient"/>.</summary>
/// <param name="client">The client; its own timeout should be infinite.</param>
/// <param name="timeout">The maximum time to wait for the whole response, body included.</param>
public sealed class HttpClientTransport(HttpClient client, TimeSpan timeout) : IHttpTransport
{
    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(ResolvedRequest request, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return SendResult.Fail(SendFailureKind.InvalidUrl, $"Invalid URL: {request.Url}");

        using var message = BuildMessage(request, uri);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            var watch = Stopwatch.StartNew();
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            var body = await response.Content.ReadAsByteArrayAsync(cts.Token);
            watch.Stop();

            var headers = response.Headers.Concat(response.Content.Headers)
                .SelectMany(h => h.Value.Select(v => new KeyValueEntry(h.Key, v)))
                .ToList();
            return SendResult.Success(new HttpResponseData(
                (int)response.StatusCode,
                response.ReasonPhrase ?? "",
                headers,
                body,
                watch.Elapsed,
                response.RequestMessage!.RequestUri!.AbsoluteUri));
        }
        catch (OperationCanceledException)
        {
            return ct.IsCancellationRequested
                ? SendResult.Fail(SendFailureKind.Canceled, "Request canceled")
                : SendResult.Fail(SendFailureKind.Timeout, $"Request timed out after {timeout.TotalSeconds:0.###} s");
        }
        catch (HttpRequestException ex)
        {
            return SendResult.Fail(SendFailureKind.ConnectionFailed, Innermost(ex).Message);
        }
        catch (Exception ex)
        {
            return SendResult.Fail(SendFailureKind.Unknown, ex.Message);
        }
    }

    private static Exception Innermost(Exception ex)
    {
        while (ex.InnerException is { } next)
            ex = next;
        return ex;
    }

    private static HttpRequestMessage BuildMessage(ResolvedRequest request, Uri uri)
    {
        var message = new HttpRequestMessage(request.Method == HttpMethodKind.Post ? HttpMethod.Post : HttpMethod.Get, uri);
        if (request.BodyText is not null)
        {
            message.Content = new StringContent(request.BodyText, Encoding.UTF8);
            message.Content.Headers.ContentType = null;
            if (request.BodyContentType is not null)
            {
                if (MediaTypeHeaderValue.TryParse(request.BodyContentType, out var parsed))
                    message.Content.Headers.ContentType = parsed;
                else
                    message.Content.Headers.TryAddWithoutValidation("Content-Type", request.BodyContentType);
            }
        }

        foreach (var header in request.Headers)
        {
            if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!message.Headers.TryAddWithoutValidation(header.Key, header.Value))
                message.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return message;
    }
}
