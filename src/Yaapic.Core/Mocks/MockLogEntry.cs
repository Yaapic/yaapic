using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Mocks;

/// <summary>One request answered by a mock server, as kept in its log.</summary>
/// <param name="At">When the request was received.</param>
/// <param name="Method">The request method.</param>
/// <param name="Target">The request target (path and query).</param>
/// <param name="Status">The response status.</param>
/// <param name="RouteName">The matched route name, or <see langword="null"/> when none matched.</param>
/// <param name="Duration">How long the request took.</param>
/// <param name="RequestHeaders">The request headers.</param>
/// <param name="RequestBodyPreview">The first 4096 characters of the request body.</param>
public sealed record MockLogEntry(
    DateTimeOffset At, string Method, string Target, int Status, string? RouteName,
    TimeSpan Duration, IReadOnlyList<KeyValueEntry> RequestHeaders, string RequestBodyPreview)
{
    private const int PreviewLength = 4096;

    /// <summary>Builds the entry of an answered request.</summary>
    /// <param name="request">The request.</param>
    /// <param name="response">The response sent.</param>
    /// <param name="routeName">The matched route name, or <see langword="null"/>.</param>
    /// <param name="at">When the request was received.</param>
    /// <param name="duration">How long the request took.</param>
    /// <returns>The log entry.</returns>
    public static MockLogEntry From(
        MockHttpRequest request, MockHttpResponse response, string? routeName, DateTimeOffset at, TimeSpan duration)
    {
        var body = Encoding.UTF8.GetString(request.Body);
        return new MockLogEntry(
            at, request.Method, request.RawTarget, response.Status, routeName, duration, request.Headers,
            body.Length > PreviewLength ? body[..PreviewLength] : body);
    }
}
