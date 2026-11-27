namespace Yaapic.Domain.Mocks;

/// <summary>The HTTP methods a mock route can match.</summary>
public static class MockMethods
{
    /// <summary>Matches every method.</summary>
    public const string Any = "ANY";

    /// <summary>Every accepted method, upper case.</summary>
    public static IReadOnlyList<string> All { get; } = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS", Any];
}
