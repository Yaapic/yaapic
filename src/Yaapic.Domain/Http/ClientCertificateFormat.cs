namespace Yaapic.Domain.Http;

/// <summary>The file format of a client certificate.</summary>
public enum ClientCertificateFormat
{
    /// <summary>A PKCS#12 bundle holding the certificate and its key.</summary>
    Pfx,

    /// <summary>A PEM certificate, with the key in the same file or in a separate key file.</summary>
    Pem,
}
