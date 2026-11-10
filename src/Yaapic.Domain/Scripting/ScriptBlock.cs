namespace Yaapic.Domain.Scripting;

/// <summary>A script in one language.</summary>
/// <param name="Language">The script language.</param>
/// <param name="Source">The source text.</param>
public sealed record ScriptBlock(ScriptLanguage Language, string Source)
{
    /// <summary>An empty JavaScript block.</summary>
    public static ScriptBlock Empty { get; } = new(ScriptLanguage.JavaScript, "");

    /// <summary>Gets a value indicating whether the source is null, empty or whitespace.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Source);
}
