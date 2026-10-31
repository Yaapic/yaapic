namespace Yaapic.UI.Abstractions.Editing;

/// <summary>A code editor control, implemented by each App project (ADR 0002).</summary>
public interface ICodeEditor
{
    /// <summary>Gets or sets the whole text of the editor.</summary>
    string Text { get; set; }

    /// <summary>Gets or sets a value indicating whether the user cannot edit the text.</summary>
    bool IsReadOnly { get; set; }

    /// <summary>Gets or sets the language used for syntax colours: <c>"json"</c>, <c>"xml"</c> or <c>"text"</c>.</summary>
    string Language { get; set; }

    /// <summary>Occurs when the user edits the text.</summary>
    event EventHandler? TextChanged;
}
