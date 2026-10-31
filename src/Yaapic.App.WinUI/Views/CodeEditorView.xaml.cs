using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.UI.Abstractions.Editing;

namespace Yaapic.App.WinUI.Views;

/// <summary>The code editor of the request and response bodies: a borderless <see cref="TextBox"/> behind <see cref="ICodeEditor"/>.</summary>
public sealed partial class CodeEditorView : UserControl, ICodeEditor
{
    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CodeEditorView),
        new PropertyMetadata("", (d, e) => ((CodeEditorView)d).OnTextChanged((string?)e.NewValue ?? "")));

    /// <summary>Identifies the <see cref="IsReadOnly"/> dependency property.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(CodeEditorView),
        new PropertyMetadata(false, (d, e) => ((CodeEditorView)d).Box.IsReadOnly = (bool)e.NewValue));

    /// <summary>Identifies the <see cref="EditorLanguage"/> dependency property.</summary>
    public static readonly DependencyProperty EditorLanguageProperty = DependencyProperty.Register(
        nameof(EditorLanguage), typeof(string), typeof(CodeEditorView), new PropertyMetadata("text"));

    /// <summary>Initializes the editor.</summary>
    public CodeEditorView() => InitializeComponent();

    /// <inheritdoc/>
    public event EventHandler? TextChanged;

    /// <inheritdoc/>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc/>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Gets or sets the language; <see cref="UIElement"/> already owns <c>Language</c>, so XAML uses this name.</summary>
    public string EditorLanguage
    {
        get => (string)GetValue(EditorLanguageProperty);
        set => SetValue(EditorLanguageProperty, value);
    }

    /// <inheritdoc/>
    string ICodeEditor.Language
    {
        get => EditorLanguage;
        set => EditorLanguage = value;
    }

    private void OnTextChanged(string text)
    {
        // The echo of a user edit finds the box already equal, which keeps the caret in place.
        if (Box.Text != text)
        {
            Box.Text = text;
        }
    }

    private void OnBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (Text != Box.Text)
        {
            Text = Box.Text;
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
