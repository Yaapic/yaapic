using System.Net;

namespace Yaapic.Infrastructure.Http;

/// <summary>Creates the <see cref="HttpClient"/> used by the application.</summary>
public static class DefaultHttpClient
{
    /// <summary>Creates a client that follows up to 10 redirects, decompresses responses and ignores cookies.</summary>
    /// <returns>A client whose own timeout is disabled; <see cref="HttpClientTransport"/> enforces the timeout.</returns>
    public static HttpClient Create() => Build(allowAutoRedirect: true);

    /// <summary>Creates a client with the same settings as <see cref="Create"/> that never follows redirects.</summary>
    /// <returns>A client returning 3xx responses untouched.</returns>
    public static HttpClient CreateNonRedirecting() => Build(allowAutoRedirect: false);

    private static HttpClient Build(bool allowAutoRedirect) =>
        new(new SocketsHttpHandler
        {
            AllowAutoRedirect = allowAutoRedirect,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
}
