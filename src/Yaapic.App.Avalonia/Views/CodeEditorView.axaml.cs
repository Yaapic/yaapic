using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using LoomEditor.Core.Model;
using LoomEditor.Themes;
using Yaapic.App.Avalonia.Presentation;
using Yaapic.UI.Abstractions.Editing;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The code editor of the request and response bodies: a LoomEditor control behind <see cref="ICodeEditor"/> (ADR 0002).</summary>
public sealed partial class CodeEditorView : UserControl, ICodeEditor
{
    /// <summary>Identifies the <see cref="Text"/> styled property.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<CodeEditorView, string>(nameof(Text), "", defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Identifies the <see cref="IsReadOnly"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<CodeEditorView, bool>(nameof(IsReadOnly));

    /// <summary>Identifies the <see cref="EditorLanguage"/> styled property.</summary>
    public static readonly StyledProperty<string> EditorLanguageProperty =
        AvaloniaProperty.Register<CodeEditorView, string>(nameof(EditorLanguage), "text");

    /// <summary>Above this many characters the text is shown as plain text: re-tokenising a multi-megabyte line freezes the UI (M2-70 measurement).</summary>
    internal const int MaxHighlightedLength = 1_000_000;

    private bool _resetting;

    /// <summary>Initializes the editor.</summary>
    public CodeEditorView()
    {
        InitializeComponent();
        Editor.Model.ContentChanged += OnModelContentChanged;
        ActualThemeVariantChanged += (_, _) => ApplyTheme();
        ApplyLanguage();
    }

    /// <inheritdoc/>
    public event EventHandler? TextChanged;

    /// <inheritdoc/>
    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <inheritdoc/>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Gets or sets the language: <c>json</c>, <c>xml</c>; anything else is plain text.</summary>
    public string EditorLanguage
    {
        get => GetValue(EditorLanguageProperty);
        set => SetValue(EditorLanguageProperty, value);
    }

    /// <inheritdoc/>
    string ICodeEditor.Language
    {
        get => EditorLanguage;
        set => EditorLanguage = value;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ApplyTheme();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            OnTextChanged(change.GetNewValue<string?>() ?? "");
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            Editor.IsReadOnly = change.GetNewValue<bool>();
        }
        else if (change.Property == EditorLanguageProperty)
        {
            ApplyLanguage();
        }
    }

    private void OnTextChanged(string text)
    {
        // The echo of a user edit finds the model already equal: no reset, so the caret and the undo stack stay.
        if (Editor.Model.GetValue() == text)
        {
            return;
        }

        _resetting = true;
        try
        {
            Editor.ResetText(text);
        }
        finally
        {
            _resetting = false;
        }

        ApplyLanguage();
    }

    private void OnModelContentChanged(object? sender, ModelContentChangedEventArgs e)
    {
        if (_resetting)
        {
            return;
        }

        var value = Editor.Model.GetValue();
        if (Text != value)
        {
            Text = value;
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyLanguage() => Editor.LanguageId = Text.Length > MaxHighlightedLength ? "plaintext" : EditorLanguage switch
    {
        "json" => "json",
        "xml" => "xml",
        _ => "plaintext",
    };

    private void ApplyTheme()
    {
        var light = ActualThemeVariant == ThemeVariant.Light;
        Color C(string key) => BrushMap.Find(this, key) is ISolidColorBrush brush ? brush.Color : default;
        static string Hex(Color c) => string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}");
        string Rule(string key, params string[] scopes) =>
            $"{{\"scope\":[{string.Join(',', scopes.Select(s => $"\"{s}\""))}],\"settings\":{{\"foreground\":\"{Hex(C(key))}\"}}}}";

        var selection = Hex(C("YaapicSelectionBackgroundBrush"));
        var json = $$"""
            {"colors":{
              "editor.background":"{{Hex(C("YaapicSurfaceSunkenBrush"))}}",
              "editor.foreground":"{{Hex(C("YaapicTextPrimaryBrush"))}}",
              "editorLineNumber.foreground":"{{Hex(C("YaapicTextDisabledBrush"))}}",
              "editorLineNumber.activeForeground":"{{Hex(C("YaapicTextSecondaryBrush"))}}",
              "editor.selectionBackground":"{{selection}}",
              "editor.lineHighlightBackground":"{{selection}}"},
             "tokenColors":[
              {{Rule("YaapicSyntaxPunctuationBrush", "punctuation", "meta.brace", "meta.structure.dictionary.json")}},
              {{Rule("YaapicSyntaxStringBrush", "string", "string.quoted.double.json")}},
              {{Rule("YaapicSyntaxKeyBrush", "support.type.property-name", "support.type.property-name.json")}},
              {{Rule("YaapicSyntaxNumberBrush", "constant.numeric")}},
              {{Rule("YaapicSyntaxBooleanBrush", "constant.language")}},
              {{Rule("YaapicSyntaxTagBrush", "entity.name.tag")}},
              {{Rule("YaapicSyntaxAttributeBrush", "entity.other.attribute-name")}},
              {{Rule("YaapicSyntaxCommentBrush", "comment")}}]}
            """;
        Editor.Theme = EditorTheme.Load(json, light ? "vs" : "vs-dark", "Yaapic", _ => null);
    }
}
