namespace Yaapic.Domain.Http;

/// <summary>A client certificate entry presented to the hosts matching <paramref name="HostPattern"/>.</summary>
/// <param name="Id">The stable identifier of the entry.</param>
/// <param name="Name">The display name.</param>
/// <param name="HostPattern">A host such as <c>api.example.com</c>, <c>*.example.com</c> or <c>host:8443</c>.</param>
/// <param name="Format">The certificate file format.</param>
/// <param name="CertificatePath">The path of the certificate file.</param>
/// <param name="KeyPath">The path of the key file; ignored for <see cref="ClientCertificateFormat.Pfx"/>, may be empty for PEM.</param>
/// <param name="HasPassphrase">Whether a passphrase is stored in the vault for this entry.</param>
public sealed record ClientCertificate(
    Guid Id,
    string Name,
    string HostPattern,
    ClientCertificateFormat Format,
    string CertificatePath,
    string KeyPath,
    bool HasPassphrase);
