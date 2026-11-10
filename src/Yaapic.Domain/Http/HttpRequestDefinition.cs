using Yaapic.Domain.Scripting;

namespace Yaapic.Domain.Http;

/// <summary>An editable, savable description of an HTTP request.</summary>
/// <param name="Id">The stable identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Method">The HTTP method.</param>
/// <param name="Url">The URL template, which may contain variables.</param>
/// <param name="QueryParams">The query parameters, in order.</param>
/// <param name="Headers">The request headers, in order.</param>
/// <param name="Body">The request body.</param>
/// <param name="Auth">The request authentication.</param>
/// <param name="Settings">The per-request settings.</param>
/// <param name="Scripts">The pre-request and post-response scripts.</param>
public sealed record HttpRequestDefinition(
    Guid Id,
    string Name,
    HttpMethodKind Method,
    string Url,
    IReadOnlyList<KeyValueEntry> QueryParams,
    IReadOnlyList<KeyValueEntry> Headers,
    RequestBody Body,
    RequestAuth Auth,
    RequestSettings Settings,
    ScriptSet Scripts = null!)
{
    /// <summary>Gets the scripts; a <see langword="null"/> constructor argument becomes <see cref="ScriptSet.Empty"/>.</summary>
    public ScriptSet Scripts { get; init; } = Scripts ?? ScriptSet.Empty;

    /// <summary>Creates an empty GET request with a new identifier.</summary>
    /// <param name="name">The display name; it is trimmed.</param>
    /// <returns>The new request.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
    public static HttpRequestDefinition Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(Guid.NewGuid(), name.Trim(), HttpMethodKind.Get, "", [], [], RequestBody.None, RequestAuth.None, RequestSettings.Default, ScriptSet.Empty);
    }

    /// <summary>Compares two requests by value, including the contents of the lists.</summary>
    /// <param name="other">The request to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool IsEquivalentTo(HttpRequestDefinition other) =>
        Id == other.Id
        && Name == other.Name
        && Method == other.Method
        && Url == other.Url
        && QueryParams.SequenceEqual(other.QueryParams)
        && Headers.SequenceEqual(other.Headers)
        && Body == other.Body
        && Auth == other.Auth
        && Settings == other.Settings
        && Scripts == other.Scripts;
}
