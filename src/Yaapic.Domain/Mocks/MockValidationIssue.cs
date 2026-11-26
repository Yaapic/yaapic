namespace Yaapic.Domain.Mocks;

/// <summary>An issue and the route it concerns.</summary>
/// <param name="Issue">The problem.</param>
/// <param name="RouteId">The route concerned; <see langword="null"/> for a server-level issue.</param>
public sealed record MockValidationIssue(MockIssue Issue, Guid? RouteId);
