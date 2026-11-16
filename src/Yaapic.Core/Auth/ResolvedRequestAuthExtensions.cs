using Yaapic.Core.Http;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth;

/// <summary>Helpers authentication methods use to add credentials to a <see cref="ResolvedRequest"/>.</summary>
public static class ResolvedRequestAuthExtensions
{
    /// <summary>Appends a header unless one with the same name (ignoring case) is already present.</summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>The request with the header, or the same request when the user already set it.</returns>
    public static ResolvedRequest WithHeaderUnlessPresent(this ResolvedRequest request, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Headers.Any(h => h.Key.Equals(name, StringComparison.OrdinalIgnoreCase))
            ? request
            : request with { Headers = [.. request.Headers, new KeyValueEntry(name, value)] };
    }

    /// <summary>Appends an escaped query parameter after the existing ones and before the fragment.</summary>
    /// <param name="request">The request.</param>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>The request with the new URL.</returns>
    public static ResolvedRequest WithQueryParameter(this ResolvedRequest request, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request with { Url = RequestResolver.AppendQuery(request.Url, [new(name, value)]) };
    }
}
