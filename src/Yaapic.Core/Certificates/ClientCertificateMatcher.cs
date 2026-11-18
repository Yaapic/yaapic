using System.Globalization;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Certificates;

/// <summary>Picks the client certificate entry that applies to a request URL.</summary>
public static class ClientCertificateMatcher
{
    /// <summary>Finds the most specific valid entry matching <paramref name="url"/>.</summary>
    /// <param name="entries">The configured entries, in list order (earlier wins a tie).</param>
    /// <param name="url">The request URL; only <c>https</c> can match.</param>
    /// <returns>The best entry, or <see langword="null"/> when none matches.</returns>
    public static ClientCertificate? Find(IReadOnlyList<ClientCertificate> entries, Uri url)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps) return null;

        ClientCertificate? best = null;
        var bestRank = int.MaxValue;
        foreach (var entry in entries)
        {
            if (ClientCertificateValidator.Validate(entry).Count > 0) continue;
            var rank = Rank(entry.HostPattern, url);
            if (rank < bestRank)
            {
                best = entry;
                bestRank = rank;
            }
        }
        return best;
    }

    private static int Rank(string pattern, Uri url)
    {
        var host = pattern;
        var hasPort = false;
        var colon = pattern.LastIndexOf(':');
        if (colon >= 0)
        {
            host = pattern[..colon];
            hasPort = true;
            if (int.Parse(pattern[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture) != url.Port) return int.MaxValue;
        }

        if (host.StartsWith("*.", StringComparison.Ordinal))
        {
            var suffix = host[1..]; // ".example.com"
            return url.Host.Length > suffix.Length && url.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? (hasPort ? 2 : 3)
                : int.MaxValue;
        }
        return string.Equals(host, url.Host, StringComparison.OrdinalIgnoreCase) ? (hasPort ? 0 : 1) : int.MaxValue;
    }
}
