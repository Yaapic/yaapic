using Yaapic.Core.Certificates;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IClientCertificateProvider"/> that maps a host to a canned lookup.</summary>
public sealed class FakeClientCertificateProvider : IClientCertificateProvider
{
    /// <summary>Gets the lookups by host (case-insensitive).</summary>
    public Dictionary<string, ClientCertificateLookup> ByHost { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the URLs looked up so far.</summary>
    public List<Uri> Requests { get; } = [];

    /// <inheritdoc/>
    public Task<ClientCertificateLookup> FindAsync(Uri url, CancellationToken ct)
    {
        Requests.Add(url);
        return Task.FromResult(ByHost.GetValueOrDefault(url.Host) ?? new ClientCertificateLookup(null, null, null));
    }
}
