namespace Yaapic.Domain.Http;

/// <summary>The algorithm used to sign a JWT.</summary>
public enum JwtAlgorithm
{
    /// <summary>HMAC using SHA-256.</summary>
    Hs256,

    /// <summary>HMAC using SHA-384.</summary>
    Hs384,

    /// <summary>HMAC using SHA-512.</summary>
    Hs512,

    /// <summary>RSASSA-PKCS1-v1_5 using SHA-256.</summary>
    Rs256,

    /// <summary>RSASSA-PKCS1-v1_5 using SHA-384.</summary>
    Rs384,

    /// <summary>RSASSA-PKCS1-v1_5 using SHA-512.</summary>
    Rs512,

    /// <summary>ECDSA using P-256 and SHA-256.</summary>
    Es256,

    /// <summary>ECDSA using P-384 and SHA-384.</summary>
    Es384,

    /// <summary>ECDSA using P-521 and SHA-512.</summary>
    Es512,
}
