namespace Yaapic.Domain.Mocks;

/// <summary>A mock server: a port and its ordered routes.</summary>
/// <param name="Id">The stable identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Port">The TCP port to listen on.</param>
/// <param name="Cors">Whether permissive CORS headers are added.</param>
/// <param name="Routes">The routes, in matching order.</param>
public sealed record MockServerDefinition(Guid Id, string Name, int Port, bool Cors, IReadOnlyList<MockRoute> Routes)
{
    /// <summary>Creates a server without route.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="port">The port to listen on.</param>
    /// <returns>The new server.</returns>
    public static MockServerDefinition Create(string name, int port = 3000) => new(Guid.NewGuid(), name, port, true, []);

    /// <summary>Compares two servers by value, including routes and headers.</summary>
    /// <param name="other">The server to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool IsEquivalentTo(MockServerDefinition other) =>
        Id == other.Id && Name == other.Name && Port == other.Port && Cors == other.Cors
        && Routes.Count == other.Routes.Count && Routes.Zip(other.Routes).All(p => p.First.IsEquivalentTo(p.Second));
}
