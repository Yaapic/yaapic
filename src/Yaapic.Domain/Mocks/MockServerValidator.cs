namespace Yaapic.Domain.Mocks;

/// <summary>Validates a <see cref="MockServerDefinition"/> without any I/O.</summary>
public static class MockServerValidator
{
    private const string TokenSymbols = "!#$%&'*+-.^_`|~";

    /// <summary>Lists the issues of a server.</summary>
    /// <param name="server">The server to check.</param>
    /// <returns>The issues found; empty for a valid server.</returns>
    public static IReadOnlyList<MockValidationIssue> Validate(MockServerDefinition server)
    {
        ArgumentNullException.ThrowIfNull(server);
        var issues = new List<MockValidationIssue>();
        if (string.IsNullOrWhiteSpace(server.Name)) issues.Add(new(MockIssue.NameEmpty, null));
        if (server.Port is < 1024 or > 65535) issues.Add(new(MockIssue.PortOutOfRange, null));
        foreach (var r in server.Routes)
        {
            if (!MockMethods.All.Contains(r.Method, StringComparer.Ordinal)) issues.Add(new(MockIssue.RouteMethodInvalid, r.Id));
            if (!IsValidPath(r.Path)) issues.Add(new(MockIssue.RoutePathInvalid, r.Id));
            if (r.Status is < 100 or > 599) issues.Add(new(MockIssue.RouteStatusOutOfRange, r.Id));
            if (r.DelayMs is < 0 or > 60000) issues.Add(new(MockIssue.RouteDelayOutOfRange, r.Id));
            if (r.Headers.Any(h => h.Enabled && !IsToken(h.Key))) issues.Add(new(MockIssue.RouteHeaderNameInvalid, r.Id));
        }
        return issues;
    }

    private static bool IsValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/') return false;
        if (path.Any(c => c is '?' or '#' || char.IsWhiteSpace(c) || char.IsControl(c))) return false;
        var segments = path.Split('/');
        for (var i = 0; i < segments.Length; i++)
            if (segments[i].Contains('*') && !(segments[i] == "*" && i == segments.Length - 1)) return false;
        return true;
    }

    private static bool IsToken(string? name) =>
        !string.IsNullOrEmpty(name) && name.All(c => char.IsAsciiLetterOrDigit(c) || TokenSymbols.Contains(c));
}
