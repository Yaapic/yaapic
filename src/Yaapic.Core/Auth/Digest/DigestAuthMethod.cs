using System.Security.Cryptography;
using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Auth.Digest;

/// <summary>Answers a Digest challenge: sends an unauthenticated probe, then adds the computed <c>Authorization</c> header.</summary>
/// <param name="transport">The transport used for the probe.</param>
/// <param name="clientNonce">Produces the client nonce.</param>
public sealed class DigestAuthMethod(IHttpTransport transport, Func<string> clientNonce) : IAuthMethod
{
    /// <summary>Initializes a new instance that uses 16 random bytes, in lowercase hex, as the client nonce.</summary>
    /// <param name="transport">The transport used for the probe.</param>
    public DigestAuthMethod(IHttpTransport transport)
        : this(transport, () => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)))
    {
    }

    /// <inheritdoc />
    public AuthKind Kind => AuthKind.Digest;

    /// <inheritdoc />
    public async Task<AuthenticationResult> ApplyAsync(ResolvedRequest request, ResolvedAuth auth, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(auth);
        if (auth.Username.Length == 0 && auth.Password.Length == 0)
        {
            return AuthenticationResult.Fail(Strings.DigestCredentialsMissing);
        }

        var probe = request with
        {
            Headers = [.. request.Headers.Where(h => !IsAuthorization(h))],
            BodyText = null,
            BodyFilePath = null,
            Parts = null,
            BodyContentType = null,
            PendingAuth = null,
        };
        var sent = await transport.SendAsync(probe, ct).ConfigureAwait(false);
        if (sent.Failure is { } failure)
        {
            return AuthenticationResult.Fail(failure.Message);
        }

        var response = sent.Response!;
        var challengeHeader = string.Join(
            ", ",
            response.Headers.Where(h => h.Key.Equals("WWW-Authenticate", StringComparison.OrdinalIgnoreCase)).Select(h => h.Value));
        if (response.StatusCode != 401 || !DigestChallenge.TryParse(challengeHeader, out var challenge))
        {
            return AuthenticationResult.Success(request);
        }

        try
        {
            var uri = new Uri(request.Url);
            var value = DigestResponse.BuildHeader(
                challenge!, request.Method.ToText(), uri.PathAndQuery, auth.Username, auth.Password, clientNonce());
            return AuthenticationResult.Success(request.WithHeaderUnlessPresent("Authorization", value));
        }
        catch (NotSupportedException ex)
        {
            return AuthenticationResult.Fail(ex.Message);
        }
    }

    private static bool IsAuthorization(KeyValueEntry h) => h.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase);
}
