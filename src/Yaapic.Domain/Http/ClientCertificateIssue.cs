namespace Yaapic.Domain.Http;

/// <summary>A problem found in a <see cref="ClientCertificate"/>.</summary>
public enum ClientCertificateIssue
{
    /// <summary>The name is empty.</summary>
    NameEmpty,

    /// <summary>The host pattern is not a valid host, wildcard host or host with port.</summary>
    HostPatternInvalid,

    /// <summary>The certificate path is empty.</summary>
    CertificatePathEmpty,

    /// <summary>A key path is required; never reported today because a PEM file may hold the key.</summary>
    KeyPathRequired,
}
