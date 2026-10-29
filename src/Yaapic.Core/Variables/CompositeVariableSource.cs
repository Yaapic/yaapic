namespace Yaapic.Core.Variables;

/// <summary>Asks several sources in order; the first hit wins.</summary>
/// <param name="sources">The sources, by decreasing priority.</param>
public sealed class CompositeVariableSource(params IVariableSource[] sources) : IVariableSource
{
    /// <inheritdoc />
    public bool TryGet(string name, out string value)
    {
        foreach (var source in sources)
        {
            if (source.TryGet(name, out value))
            {
                return true;
            }
        }

        value = "";
        return false;
    }
}
