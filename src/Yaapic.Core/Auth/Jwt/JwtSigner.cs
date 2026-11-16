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
    /// <exception cref="NotSupportedException">The algorithm is not an HMAC one.</exception>
    public static string Sign(string headerJson, string payloadJson, JwtAlgorithm algorithm, byte[] hmacKey)
    {
        ArgumentNullException.ThrowIfNull(headerJson);
        ArgumentNullException.ThrowIfNull(payloadJson);
        ArgumentNullException.ThrowIfNull(hmacKey);

        var input = Base64Url.Encode(Encoding.UTF8.GetBytes(headerJson)) + "."
            + Base64Url.Encode(Encoding.UTF8.GetBytes(payloadJson));
        var data = Encoding.ASCII.GetBytes(input);
        var signature = algorithm switch
        {
            JwtAlgorithm.Hs256 => HMACSHA256.HashData(hmacKey, data),
            JwtAlgorithm.Hs384 => HMACSHA384.HashData(hmacKey, data),
            JwtAlgorithm.Hs512 => HMACSHA512.HashData(hmacKey, data),
            _ => throw new NotSupportedException($"Algorithm {algorithm} is not supported by this signer."),
        };
        return input + "." + Base64Url.Encode(signature);
    }
}
