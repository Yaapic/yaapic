namespace Yaapic.Core.Import.Bruno;

/// <summary>The kind of content a Bru block holds.</summary>
internal enum BruBlockKind
{
    /// <summary>Key/value lines (<c>key: value</c>).</summary>
    Dictionary,

    /// <summary>Free text (bodies, scripts, tests, docs).</summary>
    Text,

    /// <summary>A comma separated list between square brackets.</summary>
    List,
}

/// <summary>One key/value line of a dictionary block.</summary>
/// <param name="Key">The key, without the disabled marker.</param>
/// <param name="Value">The value.</param>
/// <param name="Enabled">False when the line started with <c>~</c>.</param>
internal sealed record BruPair(string Key, string Value, bool Enabled);

/// <summary>One block of a Bru file.</summary>
/// <param name="Name">The block name, e.g. <c>auth:bearer</c>.</param>
/// <param name="Kind">The content kind.</param>
/// <param name="Pairs">The pairs of a dictionary block; empty otherwise.</param>
/// <param name="Text">The text of a text block; empty otherwise.</param>
/// <param name="Items">The items of a list block; empty otherwise.</param>
internal sealed record BruBlock(
    string Name,
    BruBlockKind Kind,
    IReadOnlyList<BruPair> Pairs,
    string Text,
    IReadOnlyList<string> Items);

/// <summary>The parsed blocks of a Bru file, in file order.</summary>
/// <param name="blocks">The blocks.</param>
internal sealed class BruFile(IReadOnlyList<BruBlock> blocks)
{
    /// <summary>Gets all blocks.</summary>
    public IReadOnlyList<BruBlock> Blocks { get; } = blocks;

    /// <summary>Finds the first block with the given name.</summary>
    /// <param name="name">The block name (ordinal comparison).</param>
    /// <returns>The block, or null.</returns>
    public BruBlock? Get(string name) => Blocks.FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.Ordinal));

    /// <summary>Finds every block whose name starts with a prefix.</summary>
    /// <param name="prefix">The name prefix (ordinal comparison).</param>
    /// <returns>The matching blocks, in file order.</returns>
    public IEnumerable<BruBlock> GetAll(string prefix) =>
        Blocks.Where(b => b.Name.StartsWith(prefix, StringComparison.Ordinal));
}
