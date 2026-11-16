namespace Yaapic.Domain.Http;

/// <summary>Validates <see cref="ClientCertificate"/> entries without any I/O.</summary>
public static class ClientCertificateValidator
{
    /// <summary>Lists the issues of an entry.</summary>
    /// <param name="certificate">The entry to check.</param>
    /// <returns>The issues found; empty for a valid entry.</returns>
    public static IReadOnlyList<ClientCertificateIssue> Validate(ClientCertificate certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var issues = new List<ClientCertificateIssue>();
        if (string.IsNullOrWhiteSpace(certificate.Name)) issues.Add(ClientCertificateIssue.NameEmpty);
        if (!IsValidHostPattern(certificate.HostPattern)) issues.Add(ClientCertificateIssue.HostPatternInvalid);
        if (string.IsNullOrWhiteSpace(certificate.CertificatePath)) issues.Add(ClientCertificateIssue.CertificatePathEmpty);
        return issues;
    }

    private static bool IsValidHostPattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern.Any(char.IsWhiteSpace) || pattern.Contains('/')) return false;
        var host = pattern;
        var colon = pattern.LastIndexOf(':');
        if (colon >= 0)
        {
            host = pattern[..colon];
            if (!int.TryParse(pattern[(colon + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port)
                || port is < 1 or > 65535) return false;
        }
        if (host.Length == 0 || host.Contains(':')) return false;
        var stars = host.Count(c => c == '*');
        if (stars == 0) return true;
        return stars == 1 && host.Length > 2 && host.StartsWith("*.", StringComparison.Ordinal);
    }
}
