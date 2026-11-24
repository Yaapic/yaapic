namespace Yaapic.TestDoubles;

/// <summary>Deterministic identifier sources for tests.</summary>
public static class SequentialIds
{
    /// <summary>Creates a source returning <c>00000000-0000-0000-0000-000000000001</c>, <c>...02</c> and so on.</summary>
    /// <param name="start">The first counter value.</param>
    /// <returns>The identifier source.</returns>
    public static Func<Guid> Create(int start = 1)
    {
        var next = start;
        return () => Guid.Parse($"00000000-0000-0000-0000-{next++:x12}");
    }
}
