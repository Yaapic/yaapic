namespace Yaapic.Core.CodeGeneration;

/// <summary>The available code generators.</summary>
public sealed class CodeGeneratorRegistry
{
    private readonly Dictionary<string, ICodeGenerator> _byId = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="CodeGeneratorRegistry"/> class.</summary>
    /// <param name="generators">The generators, in display order.</param>
    /// <exception cref="ArgumentException">Two generators share an id.</exception>
    public CodeGeneratorRegistry(IEnumerable<ICodeGenerator> generators)
    {
        ArgumentNullException.ThrowIfNull(generators);
        All = [.. generators];
        foreach (var g in All)
        {
            if (!_byId.TryAdd(g.Id, g))
            {
                throw new ArgumentException($"Duplicate code generator id '{g.Id}'.", nameof(generators));
            }
        }
    }

    /// <summary>Gets the generators in registration order.</summary>
    public IReadOnlyList<ICodeGenerator> All { get; }

    /// <summary>Finds a generator.</summary>
    /// <param name="id">The generator id.</param>
    /// <returns>The generator, or null when unknown.</returns>
    public ICodeGenerator? Find(string id) => _byId.GetValueOrDefault(id);
}
