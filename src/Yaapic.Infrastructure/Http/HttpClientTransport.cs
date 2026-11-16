using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Yaapic.Core.Certificates;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Infrastructure.Http;

/// <summary>An <see cref="IHttpTransport"/> built on <see cref="HttpClient"/>.</summary>
/// <param name="client">The client following redirects; its own timeout should be infinite.</param>
/// <param name="noRedirectClient">The client used when a request does not follow redirects.</param>
/// <param name="timeout">The default maximum time to wait for the whole response, body included.</param>
/// <param name="certificates">Finds the client certificate to present (mTLS); <see langword="null"/> presents none.</param>
/// <param name="configureHandler">Tests only: applied to every per-certificate handler the transport creates (e.g. to accept a self-signed server certificate); the composition passes <see langword="null"/>.</param>
public sealed class HttpClientTransport(
    HttpClient client,
    HttpClient noRedirectClient,
    TimeSpan timeout,
    IClientCertificateProvider? certificates = null,
    Action<SocketsHttpHandler>? configureHandler = null) : IHttpTransport, IDisposable
{
    private sealed record CertificateClients(X509Certificate2 Certificate, HttpClient Follow, HttpClient NoFollow);

    private readonly ConcurrentDictionary<string, CertificateClients> _certificateClients = new();

    /// <summary>The number of per-certificate client pairs currently cached.</summary>
    internal int CachedCertificateClients => _certificateClients.Count;

    /// <summary>Disposes the per-certificate clients; the default clients belong to the caller.</summary>
    public void Dispose()
    {
        foreach (var key in _certificateClients.Keys)
            if (_certificateClients.TryRemove(key, out var pair))
                DisposePair(pair);
    }

    private static void DisposePair(CertificateClients pair)
    {
        pair.Follow.Dispose();
        pair.NoFollow.Dispose();
    }

    private CertificateClients GetClients(string key, X509Certificate2 certificate)
    {
        if (_certificateClients.TryGetValue(key, out var existing))
        {
            if (ReferenceEquals(existing.Certificate, certificate))
                return existing;
            if (_certificateClients.TryRemove(new KeyValuePair<string, CertificateClients>(key, existing)))
                DisposePair(existing);
        }

        var created = new CertificateClients(
            certificate,
            DefaultHttpClient.Create(certificate, configureHandler),
            DefaultHttpClient.CreateNonRedirecting(certificate, configureHandler));
        var winner = _certificateClients.GetOrAdd(key, created);
        if (!ReferenceEquals(winner, created))
            DisposePair(created);
        return winner;
    }

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(ResolvedRequest request, CancellationToken ct)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return SendResult.Fail(SendFailureKind.InvalidUrl, $"Invalid URL: {request.Url}");

        var followClient = client;
        var noFollowClient = noRedirectClient;
        if (certificates is not null)
        {
            var lookup = await certificates.FindAsync(uri, ct);
            if (lookup.Error is not null || (lookup.CacheKey is not null && lookup.Certificate is null))
                return SendResult.Fail(SendFailureKind.ClientCertificateError, lookup.Error ?? "The client certificate could not be loaded");
            if (lookup is { CacheKey: { } key, Certificate: { } certificate })
            {
                var pair = GetClients(key, certificate);
                followClient = pair.Follow;
                noFollowClient = pair.NoFollow;
            }
        }

        var message = BuildMessage(request, uri, out var unreadable);
        if (unreadable is not null)
            return unreadable;

        using var _ = message;
        var effectiveTimeout = request.TimeoutSeconds > 0 ? TimeSpan.FromSeconds(request.TimeoutSeconds) : timeout;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(effectiveTimeout);
        var http = request.FollowRedirects ? followClient : noFollowClient;

        try
        {
            var watch = Stopwatch.StartNew();
            using var response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cts.Token);
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
                : SendResult.Fail(SendFailureKind.Timeout, $"Request timed out after {effectiveTimeout.TotalSeconds:0.###} s");
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

    private static HttpMethod MapMethod(HttpMethodKind kind) => kind switch
    {
        HttpMethodKind.Post => HttpMethod.Post,
        HttpMethodKind.Put => HttpMethod.Put,
        HttpMethodKind.Patch => HttpMethod.Patch,
        HttpMethodKind.Delete => HttpMethod.Delete,
        HttpMethodKind.Head => HttpMethod.Head,
        HttpMethodKind.Options => HttpMethod.Options,
        _ => HttpMethod.Get,
    };

    private static HttpRequestMessage BuildMessage(ResolvedRequest request, Uri uri, out SendResult? unreadable)
    {
        unreadable = null;
        var message = new HttpRequestMessage(MapMethod(request.Method), uri);
        var path = "";
        try
        {
            if (request.Parts is not null)
            {
                var form = new MultipartFormDataContent();
                message.Content = form;
                foreach (var part in request.Parts)
                {
                    if (part.FilePath is null)
                    {
                        form.Add(new StringContent(part.Value ?? "", Encoding.UTF8), part.Name);
                        continue;
                    }

                    path = part.FilePath;
                    var content = new StreamContent(File.OpenRead(path));
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                    form.Add(content, part.Name, Path.GetFileName(path));
                }
            }
            else if (request.BodyFilePath is not null)
            {
                path = request.BodyFilePath;
                message.Content = new StreamContent(File.OpenRead(path));
                SetContentType(message.Content, request.BodyContentType);
            }
            else if (request.BodyText is not null)
            {
                message.Content = new StringContent(request.BodyText, Encoding.UTF8);
                SetContentType(message.Content, request.BodyContentType);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            message.Dispose();
            unreadable = SendResult.Fail(SendFailureKind.FileUnreadable, $"Cannot read file '{path}': {ex.Message}");
            return message;
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

    private static void SetContentType(HttpContent content, string? contentType)
    {
        content.Headers.ContentType = null;
        if (contentType is null)
            return;
        if (MediaTypeHeaderValue.TryParse(contentType, out var parsed))
            content.Headers.ContentType = parsed;
        else
            content.Headers.TryAddWithoutValidation("Content-Type", contentType);
    }
}
