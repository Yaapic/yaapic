using System.Net;

namespace Yaapic.Infrastructure.Http;

/// <summary>Creates the <see cref="HttpClient"/> used by the application.</summary>
public static class DefaultHttpClient
{
    /// <summary>Creates a client that follows up to 10 redirects, decompresses responses and ignores cookies.</summary>
    /// <returns>A client whose own timeout is disabled; <see cref="HttpClientTransport"/> enforces the timeout.</returns>
    public static HttpClient Create() =>
        new(new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
        })
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
}
