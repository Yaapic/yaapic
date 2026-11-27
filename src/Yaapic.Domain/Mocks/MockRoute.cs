using Yaapic.Domain.Http;

namespace Yaapic.Domain.Mocks;

/// <summary>One route of a mock server: what it matches and the response it returns.</summary>
/// <param name="Id">The stable identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Enabled">Whether the route takes part in matching.</param>
/// <param name="Method">The matched method, one of <see cref="MockMethods.All"/>.</param>
/// <param name="Path">The matched path, starting with <c>/</c>.</param>
/// <param name="Status">The response status code.</param>
/// <param name="Headers">The response headers.</param>
/// <param name="Body">The response body template.</param>
/// <param name="DelayMs">The delay before answering, in milliseconds.</param>
public sealed record MockRoute(
    Guid Id, string Name, bool Enabled, string Method, string Path, int Status,
    IReadOnlyList<KeyValueEntry> Headers, string Body, int DelayMs)
{
    /// <summary>Creates a route answering <c>GET /</c> with <c>200 {}</c>.</summary>
    /// <param name="name">The display name.</param>
    /// <returns>The new route.</returns>
    public static MockRoute Create(string name) =>
        new(Guid.NewGuid(), name, true, "GET", "/", 200, [new("Content-Type", "application/json")], "{}", 0);

    /// <summary>Compares two routes by value, including the headers.</summary>
    /// <param name="other">The route to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool IsEquivalentTo(MockRoute other) =>
        Id == other.Id && Name == other.Name && Enabled == other.Enabled && Method == other.Method && Path == other.Path
        && Status == other.Status && Body == other.Body && DelayMs == other.DelayMs && Headers.SequenceEqual(other.Headers);
}
