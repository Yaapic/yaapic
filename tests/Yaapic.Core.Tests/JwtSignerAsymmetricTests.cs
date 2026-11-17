using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Yaapic.Core.Auth.Jwt;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class JwtSignerAsymmetricTests
{
    private const string Header = "{\"alg\":\"RS256\"}";
    private const string Payload = "{\"iss\":\"joe\",\r\n \"exp\":1300819380,\r\n \"http://example.com/is_root\":true}";

    // RSA key from RFC 7515 appendix A.2 (JWK members, braces omitted).
    private const string Rfc7515A2Jwk = "{\"kty\":\"RSA\",\"n\":\"ofgWCuLjybRlzo0tZWJjNiuSfb4p4fAkd_wWJcyQoTbji9k0l8W26mPddxHmfHQp-Vaw-4qPCJrcS2mJPMEzP1Pt0Bm4d4QlL-yRT-SFd2lZS-pCgNMsD1W_YpRPEwOWvG6b32690r2jZ47soMZo9wGzjb_7OMg0LOL-bSf63kpaSHSXndS5z5rexMdbBYUsLA9e-KXBdQOS-UTo7WTBEMa2R2CapHg665xsmtdVMTBQY4uDZlxvb3qCo5ZwKh9kG4LT6_I5IhlJH7aGhyxXFvUK-DWNmoudF8NAco9_h9iaGNj8q2ethFkMLs91kzk2PAcDTW9gb54h4FRWyuXpoQ\",\"e\":\"AQAB\",\"d\":\"Eq5xpGnNCivDflJsRQBXHx1hdR1k6Ulwe2JZD50LpXyWPEAeP88vLNO97IjlA7_GQ5sLKMgvfTeXZx9SE-7YwVol2NXOoAJe46sui395IW_GO-pWJ1O0BkTGoVEn2bKVRUCgu-GjBVaYLU6f3l9kJfFNS3E0QbVdxzubSu3Mkqzjkn439X0M_V51gfpRLI9JYanrC4D4qAdGcopV_0ZHHzQlBjudU2QvXt4ehNYTCBr6XCLQUShb1juUO1ZdiYoFaFQT5Tw8bGUl_x_jTj3ccPDVZFD9pIuhLhBOneufuBiB4cS98l2SR_RQyGWSeWjnczT0QU91p1DhOVRuOopznQ\",\"p\":\"4BzEEOtIpmVdVEZNCqS7baC4crd0pqnRH_5IB3jw3bcxGn6QLvnEtfdUdiYrqBdss1l58BQ3KhooKeQTa9AB0Hw_Py5PJdTJNPY8cQn7ouZ2KKDcmnPGBY5t7yLc1QlQ5xHdwW1VhvKn-nXqhJTBgIPgtldC-KDV5z-y2XDwGUc\",\"q\":\"uQPEfgmVtjL0Uyyx88GZFF1fOunH3-7cepKmtH4pxhtCoHqpWmT8YAmZxaewHgHAjLYsp1ZSe7zFYHj7C6ul7TjeLQeZD_YwD66t62wDmpe_HlB-TnBA-njbglfIsRLtXlnDzQkv5dTltRJ11BKBBypeeF6689rjcJIDEz9RWdc\",\"dp\":\"BwKfV3Akq5_MFZDFZCnW-wzl-CCo83WoZvnLQwCTeDv8uzluRSnm71I3QCLdhrqE2e9YkxvuxdBfpT_PI7Yz-FOKnu1R6HsJeDCjn12Sk3vmAktV2zb34MCdy7cpdTh_YVr7tss2u6vneTwrA86rZtu5Mbr1C1XsmvkxHQAdYo0\",\"dq\":\"h_96-mK1R_7glhsum81dZxjTnYynPbZpHziZjeeHcXYsXaaMwkOlODsWa7I9xXDoRwbKgB719rrmI2oKr6N3Do9U0ajaHF-NKJnwgjMd2w9cjz3_-kyNlxAr2v4IKhGNpmM5iIgOS1VZnOZ68m6_pbLBSp3nssTdlqvd0tIiTHU\",\"qi\":\"IYd7DHOhrWvxkwPQsRM2tOgrjbcrfvtQJipd-DlcxyVuuM9sQLdgjVk2oy26F0EmpScGLq2MowX7fhd_QJQ3ydy5cY7YIBi87w93IKLEdfnbJtoOPLUW0ITrJReOgo1cq9SbsxYawBgfp_gh6A5603k2-ZQwVK0JKSHuLFkuQ3U\"}";

    private const string Rfc7515A2Signature = "cC4hiUPoj9Eetdgtv3hF80EGrhuB__dzERat0XF9g2VtQgr9PJbu3XOiZj5RZmh7AAuHIm4Bh-0Qc_lF5YKt_O8W2Fp5jujGbds9uJdbF9CUAr7t1dnZcAcQjbKBYNX4BAynRFdiuB--f_nZLgrnbyTyWzO75vRK5h6xBArLIARNPvkSjtQBMHlb1L07Qe7K0GarZRmB_eSN9383LcOLn6_dO--xi12jzDwusC-eOkHWEsqtFZESc6BfI7noOPqvhJ1phCnvWh6IeYI2w9QOYEUipUTI8np6LbgGY9Fs98rqVt5AXLIhWkWywlVmtVrBp0igcN_IoypGlUPQGe77Rw";

    private static RSAParameters Rfc7515A2Key()
    {
        using var doc = JsonDocument.Parse(Rfc7515A2Jwk);
        byte[] Field(string name) => Base64Url.Decode(doc.RootElement.GetProperty(name).GetString()!);
        return new RSAParameters
        {
            Modulus = Field("n"),
            Exponent = Field("e"),
            D = Field("d"),
            P = Field("p"),
            Q = Field("q"),
            DP = Field("dp"),
            DQ = Field("dq"),
            InverseQ = Field("qi"),
        };
    }

    private static (string Input, byte[] Signature) Split(string token)
    {
        var last = token.LastIndexOf('.');
        return (token[..last], Base64Url.Decode(token[(last + 1)..]));
    }

    [Fact]
    public void Rfc7515_a2_published_signature_verifies_with_the_public_key()
    {
        var parameters = Rfc7515A2Key();
        using var publicKey = RSA.Create(new RSAParameters { Modulus = parameters.Modulus, Exponent = parameters.Exponent });
        var input = "eyJhbGciOiJSUzI1NiJ9.eyJpc3MiOiJqb2UiLA0KICJleHAiOjEzMDA4MTkzODAsDQogImh0dHA6Ly9leGFtcGxlLmNvbS9pc19yb290Ijp0cnVlfQ";
        Assert.True(publicKey.VerifyData(
            Encoding.ASCII.GetBytes(input), Base64Url.Decode(Rfc7515A2Signature),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Rfc7515_a2_token_is_reproduced_from_the_private_key()
    {
        using var key = RSA.Create(Rfc7515A2Key());
        var token = JwtSigner.Sign(Header, Payload, JwtAlgorithm.Rs256, key.ExportPkcs8PrivateKeyPem());
        Assert.EndsWith("." + Rfc7515A2Signature, token);
    }

    [Theory]
    [InlineData(JwtAlgorithm.Rs256, "SHA256")]
    [InlineData(JwtAlgorithm.Rs384, "SHA384")]
    [InlineData(JwtAlgorithm.Rs512, "SHA512")]
    public void Rsa_algorithms_sign_and_verify_with_pkcs8_and_pkcs1_pem(JwtAlgorithm algorithm, string hash)
    {
        using var key = RSA.Create(2048);
        foreach (var pem in new[] { key.ExportPkcs8PrivateKeyPem(), key.ExportRSAPrivateKeyPem() })
        {
            var (input, signature) = Split(JwtSigner.Sign("{}", "{}", algorithm, pem));
            Assert.True(key.VerifyData(
                Encoding.ASCII.GetBytes(input), signature, new HashAlgorithmName(hash), RSASignaturePadding.Pkcs1));
        }
    }

    [Theory]
    [InlineData(JwtAlgorithm.Es256, "nistP256", "SHA256", 64)]
    [InlineData(JwtAlgorithm.Es384, "nistP384", "SHA384", 96)]
    [InlineData(JwtAlgorithm.Es512, "nistP521", "SHA512", 132)]
    public void Ecdsa_algorithms_sign_in_p1363_format_and_verify(JwtAlgorithm algorithm, string curve, string hash, int length)
    {
        using var key = ECDsa.Create(ECCurve.CreateFromFriendlyName(curve));
        var (input, signature) = Split(JwtSigner.Sign("{}", "{}", algorithm, key.ExportPkcs8PrivateKeyPem()));
        Assert.Equal(length, signature.Length);
        Assert.True(key.VerifyData(
            Encoding.ASCII.GetBytes(input), signature, new HashAlgorithmName(hash),
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [Theory]
    [InlineData(JwtAlgorithm.Es256, "nistP384")]
    [InlineData(JwtAlgorithm.Es384, "nistP256")]
    [InlineData(JwtAlgorithm.Es512, "nistP256")]
    public void Ecdsa_rejects_a_curve_that_does_not_match(JwtAlgorithm algorithm, string curve)
    {
        using var key = ECDsa.Create(ECCurve.CreateFromFriendlyName(curve));
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtSigner.Sign("{}", "{}", algorithm, key.ExportPkcs8PrivateKeyPem()));
        Assert.Contains("does not match", ex.Message);
    }

    [Theory]
    [InlineData(JwtAlgorithm.Rs256)]
    [InlineData(JwtAlgorithm.Es256)]
    public void Garbage_pem_is_rejected_without_leaking_it(JwtAlgorithm algorithm)
    {
        const string garbage = "-----BEGIN PRIVATE KEY-----SECRETGARBAGE-----END PRIVATE KEY-----";
        var ex = Assert.Throws<InvalidOperationException>(() => JwtSigner.Sign("{}", "{}", algorithm, garbage));
        Assert.DoesNotContain("SECRETGARBAGE", ex.Message);
        Assert.Throws<InvalidOperationException>(() => JwtSigner.Sign("{}", "{}", algorithm, ""));
    }

    [Fact]
    public void Overloads_reject_the_wrong_algorithm_family()
    {
        using var key = RSA.Create(2048);
        var pem = key.ExportPkcs8PrivateKeyPem();
        var fromPem = Assert.Throws<ArgumentException>(() => JwtSigner.Sign("{}", "{}", JwtAlgorithm.Hs256, pem));
        Assert.Contains("Hs256", fromPem.Message);
        var fromBytes = Assert.Throws<ArgumentException>(() => JwtSigner.Sign("{}", "{}", JwtAlgorithm.Rs256, [1]));
        Assert.Contains("Rs256", fromBytes.Message);
    }
}
