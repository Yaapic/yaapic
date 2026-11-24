namespace Yaapic.Core.Import;

/// <summary>The kind of a <see cref="DataNode"/>.</summary>
public enum DataKind
{
    /// <summary>A null value.</summary>
    Null,

    /// <summary>A boolean.</summary>
    Bool,

    /// <summary>A number, kept as its source text.</summary>
    Number,

    /// <summary>A string.</summary>
    String,

    /// <summary>An ordered list of nodes.</summary>
    Array,

    /// <summary>An ordered list of named members; duplicate keys are kept.</summary>
    Object,
}
