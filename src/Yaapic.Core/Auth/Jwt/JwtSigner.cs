using System.Security.Cryptography;
using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Auth.Jwt;

/// <summary>Produces compact JWTs (RFC 7519) from a ready-made header and payload.</summary>
public sealed class JwtSigner
{
    /// <summary>Signs a header and payload with an HMAC algorithm.</summary>
    /// <param name="headerJson">The JSON header, used as given.</param>
    /// <param name="payloadJson">The JSON payload, used as given.</param>
    /// <param name="algorithm">The signing algorithm.</param>
    /// <param name="hmacKey">The HMAC secret.</param>
    /// <returns>The compact token <c>header.payload.signature</c>.</returns>
    /// <exception cref="ArgumentException">The algorithm is not an HMAC one.</exception>
    public static string Sign(string headerJson, string payloadJson, JwtAlgorithm algorithm, byte[] hmacKey)
    {
        ArgumentNullException.ThrowIfNull(headerJson);
        ArgumentNullException.ThrowIfNull(payloadJson);
        ArgumentNullException.ThrowIfNull(hmacKey);

        var input = SigningInput(headerJson, payloadJson);
        var data = Encoding.ASCII.GetBytes(input);
        var signature = algorithm switch
        {
            JwtAlgorithm.Hs256 => HMACSHA256.HashData(hmacKey, data),
            JwtAlgorithm.Hs384 => HMACSHA384.HashData(hmacKey, data),
            JwtAlgorithm.Hs512 => HMACSHA512.HashData(hmacKey, data),
            _ => throw new ArgumentException($"Algorithm {algorithm} needs a PEM private key, not an HMAC secret.", nameof(algorithm)),
        };
        return input + "." + Base64Url.Encode(signature);
    }

    /// <summary>Signs a header and payload with an RSA or ECDSA algorithm.</summary>
    /// <param name="headerJson">The JSON header, used as given.</param>
    /// <param name="payloadJson">The JSON payload, used as given.</param>
    /// <param name="algorithm">The signing algorithm (RS256/384/512 or ES256/384/512).</param>
    /// <param name="privateKeyPem">The private key in PEM (PKCS#1, PKCS#8 or SEC1).</param>
    /// <returns>The compact token <c>header.payload.signature</c>.</returns>
    /// <exception cref="ArgumentException">The algorithm is an HMAC one.</exception>
    /// <exception cref="InvalidOperationException">The PEM is empty or invalid, or the curve does not match the algorithm.</exception>
    public static string Sign(string headerJson, string payloadJson, JwtAlgorithm algorithm, string privateKeyPem)
    {
        ArgumentNullException.ThrowIfNull(headerJson);
        ArgumentNullException.ThrowIfNull(payloadJson);
        ArgumentNullException.ThrowIfNull(privateKeyPem);

        var input = SigningInput(headerJson, payloadJson);
        var data = Encoding.ASCII.GetBytes(input);
        byte[] signature;
        switch (algorithm)
        {
            case JwtAlgorithm.Rs256 or JwtAlgorithm.Rs384 or JwtAlgorithm.Rs512:
                using (var rsa = RSA.Create())
                {
                    Import(() => rsa.ImportFromPem(privateKeyPem));
                    signature = rsa.SignData(data, HashFor(algorithm), RSASignaturePadding.Pkcs1);
                }

                break;
            case JwtAlgorithm.Es256 or JwtAlgorithm.Es384 or JwtAlgorithm.Es512:
                using (var ecdsa = ECDsa.Create())
                {
                    Import(() => ecdsa.ImportFromPem(privateKeyPem));
                    var expectedBits = algorithm switch { JwtAlgorithm.Es256 => 256, JwtAlgorithm.Es384 => 384, _ => 521 };
                    if (ecdsa.KeySize != expectedBits)
                    {
                        throw new InvalidOperationException($"The key does not match {algorithm}.");
                    }

                    signature = ecdsa.SignData(data, HashFor(algorithm), DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
                }

                break;
            default:
                throw new ArgumentException($"Algorithm {algorithm} needs an HMAC secret, not a PEM private key.", nameof(algorithm));
        }

        return input + "." + Base64Url.Encode(signature);
    }

    private static string SigningInput(string headerJson, string payloadJson) =>
        Base64Url.Encode(Encoding.UTF8.GetBytes(headerJson)) + "." + Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson));

    private static HashAlgorithmName HashFor(JwtAlgorithm algorithm) => algorithm switch
    {
        JwtAlgorithm.Rs256 or JwtAlgorithm.Es256 => HashAlgorithmName.SHA256,
        JwtAlgorithm.Rs384 or JwtAlgorithm.Es384 => HashAlgorithmName.SHA384,
        _ => HashAlgorithmName.SHA512,
    };

    // The inner exception is dropped on purpose: it could echo key material.
    private static void Import(Action import)
    {
        try
        {
            import();
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException("The private key is not a valid PEM key.");
        }
    }
}
