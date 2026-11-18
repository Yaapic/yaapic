using System.Globalization;

namespace Yaapic.Core.Import;

/// <summary>An immutable, ordered, loosely typed node of a foreign JSON or YAML document.</summary>
public sealed class DataNode
{
    private static readonly IReadOnlyList<DataNode> NoItems = [];
    private static readonly IReadOnlyList<KeyValuePair<string, DataNode>> NoMembers = [];

    private readonly string? _text;
    private readonly bool _bool;

    private DataNode(
        DataKind kind,
        string? text = null,
        bool boolean = false,
        IReadOnlyList<DataNode>? items = null,
        IReadOnlyList<KeyValuePair<string, DataNode>>? members = null)
    {
        Kind = kind;
        _text = text;
        _bool = boolean;
        Items = items ?? NoItems;
        Members = members ?? NoMembers;
    }

    /// <summary>Gets the null node.</summary>
    public static DataNode Null { get; } = new(DataKind.Null);

    /// <summary>Gets the kind of this node.</summary>
    public DataKind Kind { get; }

    /// <summary>Gets the items of an Array node; empty for any other kind.</summary>
    public IReadOnlyList<DataNode> Items { get; }

    /// <summary>Gets the members of an Object node in source order; empty for any other kind.</summary>
    public IReadOnlyList<KeyValuePair<string, DataNode>> Members { get; }

    /// <summary>Gets a value indicating whether this node is null.</summary>
    public bool IsNull => Kind == DataKind.Null;

    /// <summary>Gets a value indicating whether this node is an Object.</summary>
    public bool IsObject => Kind == DataKind.Object;

    /// <summary>Gets a value indicating whether this node is an Array.</summary>
    public bool IsArray => Kind == DataKind.Array;

    /// <summary>Gets a value indicating whether this node is a String.</summary>
    public bool IsString => Kind == DataKind.String;

    /// <summary>Gets the last member with the given key (ordinal).</summary>
    /// <param name="key">The member name.</param>
    /// <returns>The member value, or null when absent or when this node is not an Object.</returns>
    public DataNode? this[string key]
    {
        get
        {
            for (var i = Members.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Members[i].Key, key, StringComparison.Ordinal))
                {
                    return Members[i].Value;
                }
            }

            return null;
        }
    }

    /// <summary>Gets the item at an index.</summary>
    /// <param name="index">The zero-based index.</param>
    /// <returns>The item, or null when out of range or when this node is not an Array.</returns>
    public DataNode? this[int index] => (uint)index < (uint)Items.Count ? Items[index] : null;

    /// <summary>Creates a Bool node.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The node.</returns>
    public static DataNode Bool(bool value) => new(DataKind.Bool, boolean: value);

    /// <summary>Creates a Number node that keeps its source text.</summary>
    /// <param name="text">The number as written in the source.</param>
    /// <returns>The node.</returns>
    public static DataNode Number(string text) => new(DataKind.Number, text);

    /// <summary>Creates a String node.</summary>
    /// <param name="value">The text.</param>
    /// <returns>The node.</returns>
    public static DataNode String(string value) => new(DataKind.String, value);

    /// <summary>Creates an Array node.</summary>
    /// <param name="items">The items.</param>
    /// <returns>The node.</returns>
    public static DataNode Array(IReadOnlyList<DataNode> items) => new(DataKind.Array, items: items);

    /// <summary>Creates an Object node; members keep their order and duplicate keys are kept.</summary>
    /// <param name="members">The members.</param>
    /// <returns>The node.</returns>
    public static DataNode Object(IReadOnlyList<KeyValuePair<string, DataNode>> members) =>
        new(DataKind.Object, members: members);

    /// <summary>Reads this node as text.</summary>
    /// <returns>The string, the number source text, <c>true</c> or <c>false</c>; null for other kinds.</returns>
    public string? AsString() => Kind switch
    {
        DataKind.String or DataKind.Number => _text,
        DataKind.Bool => _bool ? "true" : "false",
        _ => null,
    };

    /// <summary>Reads this node as a boolean.</summary>
    /// <returns>The boolean, or null (the strings <c>true</c> and <c>false</c> are accepted ignoring case).</returns>
    public bool? AsBool() => Kind switch
    {
        DataKind.Bool => _bool,
        DataKind.String when string.Equals(_text, "true", StringComparison.OrdinalIgnoreCase) => true,
        DataKind.String when string.Equals(_text, "false", StringComparison.OrdinalIgnoreCase) => false,
        _ => null,
    };

    /// <summary>Reads this node as a double, with the invariant culture.</summary>
    /// <returns>The number, or null when this is not a Number or a numeric String.</returns>
    public double? AsDouble() =>
        Kind is DataKind.Number or DataKind.String
        && double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;

    /// <summary>Reads a member as text.</summary>
    /// <param name="key">The member name.</param>
    /// <returns>The text, or null.</returns>
    public string? GetString(string key) => this[key]?.AsString();

    /// <summary>Reads a member as a boolean.</summary>
    /// <param name="key">The member name.</param>
    /// <returns>The boolean, or null.</returns>
    public bool? GetBool(string key) => this[key]?.AsBool();

    /// <summary>Walks nested objects and reads the last step as text.</summary>
    /// <param name="path">The member names, outermost first.</param>
    /// <returns>The text, or null when any step is missing.</returns>
    public string? GetString(params ReadOnlySpan<string> path)
    {
        var node = this;
        foreach (var key in path)
        {
            node = node[key];
            if (node is null)
            {
                return null;
            }
        }

        return node.AsString();
    }
}
