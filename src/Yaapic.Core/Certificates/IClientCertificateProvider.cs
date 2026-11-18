namespace Yaapic.Core.Certificates;

/// <summary>Finds and loads the client certificate to present to a URL.</summary>
public interface IClientCertificateProvider
{
    /// <summary>Looks up the certificate for <paramref name="url"/>.</summary>
    /// <param name="url">The request URL.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The lookup result; the provider owns the certificate lifetime, callers never dispose it.</returns>
    Task<ClientCertificateLookup> FindAsync(Uri url, CancellationToken ct);
}
