using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Yaapic.Infrastructure.Http;

/// <summary>Creates the <see cref="HttpClient"/> used by the application.</summary>
public static class DefaultHttpClient
{
    /// <summary>Creates a client that follows up to 10 redirects, decompresses responses and ignores cookies.</summary>
    /// <returns>A client whose own timeout is disabled; <see cref="HttpClientTransport"/> enforces the timeout.</returns>
    public static HttpClient Create() => Build(allowAutoRedirect: true, null, null);

    /// <summary>Creates a client with the same settings as <see cref="Create()"/> that never follows redirects.</summary>
    /// <returns>A client returning 3xx responses untouched.</returns>
    public static HttpClient CreateNonRedirecting() => Build(allowAutoRedirect: false, null, null);

    /// <summary>Creates a following client that presents <paramref name="certificate"/> during the TLS handshake.</summary>
    /// <param name="certificate">The client certificate, with its private key.</param>
    /// <param name="configureHandler">Optional hook applied to the handler (tests only).</param>
    /// <returns>A client as <see cref="Create()"/> with the certificate attached.</returns>
    public static HttpClient Create(X509Certificate2 certificate, Action<SocketsHttpHandler>? configureHandler = null) =>
        Build(allowAutoRedirect: true, certificate, configureHandler);

    /// <summary>Creates a non-redirecting client that presents <paramref name="certificate"/> during the TLS handshake.</summary>
    /// <param name="certificate">The client certificate, with its private key.</param>
    /// <param name="configureHandler">Optional hook applied to the handler (tests only).</param>
    /// <returns>A client as <see cref="CreateNonRedirecting()"/> with the certificate attached.</returns>
    public static HttpClient CreateNonRedirecting(X509Certificate2 certificate, Action<SocketsHttpHandler>? configureHandler = null) =>
        Build(allowAutoRedirect: false, certificate, configureHandler);

    private static HttpClient Build(bool allowAutoRedirect, X509Certificate2? certificate, Action<SocketsHttpHandler>? configureHandler)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            ConnectCallback = TimingCollector.ConnectAsync,
            PlaintextStreamFilter = TimingCollector.MarkTlsAsync,
        };
        if (certificate is not null)
            handler.SslOptions = new SslClientAuthenticationOptions { ClientCertificates = [certificate] };
        configureHandler?.Invoke(handler);
        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }
}
