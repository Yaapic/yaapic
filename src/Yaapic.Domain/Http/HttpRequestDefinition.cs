namespace Yaapic.Domain.Http;

public sealed record HttpRequestDefinition(
    Guid Id,
    string Name,
    HttpMethodKind Method,
    string Url,
    IReadOnlyList<KeyValueEntry> QueryParams,
    IReadOnlyList<KeyValueEntry> Headers,
    RequestBody Body)
{
    public static HttpRequestDefinition Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(Guid.NewGuid(), name.Trim(), HttpMethodKind.Get, "", [], [], RequestBody.None);
    }

    public bool IsEquivalentTo(HttpRequestDefinition other) =>
        Id == other.Id
        && Name == other.Name
        && Method == other.Method
        && Url == other.Url
        && QueryParams.SequenceEqual(other.QueryParams)
        && Headers.SequenceEqual(other.Headers)
        && Body == other.Body;
}
