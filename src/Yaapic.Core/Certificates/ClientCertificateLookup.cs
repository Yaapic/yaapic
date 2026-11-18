using System.Security.Cryptography.X509Certificates;

namespace Yaapic.Core.Certificates;

/// <summary>The outcome of a client certificate lookup.</summary>
/// <param name="CacheKey">The matching entry id; <see langword="null"/> when no entry matches.</param>
/// <param name="Certificate">The loaded certificate; <see langword="null"/> with an <paramref name="Error"/> when the entry matched but could not be loaded.</param>
/// <param name="Error">A user-presentable message when loading failed.</param>
public sealed record ClientCertificateLookup(string? CacheKey, X509Certificate2? Certificate, string? Error);
