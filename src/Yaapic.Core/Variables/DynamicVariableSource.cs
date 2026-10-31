using System.Globalization;

namespace Yaapic.Core.Variables;

/// <summary>Resolves the built-in dynamic variables (<c>$guid</c>, <c>$timestamp</c>, <c>$isoTimestamp</c>, <c>$randomInt</c>).</summary>
/// <param name="time">The clock used for the timestamps.</param>
/// <param name="newGuid">Produces the value of <c>$guid</c>.</param>
/// <param name="random">Produces the value of <c>$randomInt</c>.</param>
public sealed class DynamicVariableSource(TimeProvider time, Func<Guid> newGuid, Random random) : IVariableSource
{
    /// <summary>Creates a source over the system clock, <see cref="Guid.NewGuid"/> and <see cref="Random.Shared"/>.</summary>
    public DynamicVariableSource()
        : this(TimeProvider.System, Guid.NewGuid, Random.Shared)
    {
    }

    /// <inheritdoc />
    public bool TryGet(string name, out string value)
    {
        string? found = name switch
        {
            "$guid" => newGuid().ToString("D"),
            "$timestamp" => time.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            "$isoTimestamp" => time.GetUtcNow().UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
            "$randomInt" => random.Next(0, 1001).ToString(CultureInfo.InvariantCulture),
            _ => null,
        };

        value = found ?? "";
        return found is not null;
    }
}
