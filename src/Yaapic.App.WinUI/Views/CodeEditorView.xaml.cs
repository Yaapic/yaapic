using System.Globalization;
using LoomEditor.Core.Model;
using LoomEditor.Themes;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Yaapic.App.WinUI.Presentation;
using Yaapic.UI.Abstractions.Editing;

namespace Yaapic.App.WinUI.Views;

/// <summary>The code editor of the request and response bodies: a LoomEditor control behind <see cref="ICodeEditor"/> (ADR 0002).</summary>
public sealed partial class CodeEditorView : UserControl, ICodeEditor
{
    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(CodeEditorView),
        new PropertyMetadata("", (d, e) => ((CodeEditorView)d).OnTextChanged((string?)e.NewValue ?? "")));

    /// <summary>Identifies the <see cref="IsReadOnly"/> dependency property.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(CodeEditorView),
        new PropertyMetadata(false, (d, e) => ((CodeEditorView)d).Editor.IsReadOnly = (bool)e.NewValue));

    /// <summary>Identifies the <see cref="EditorLanguage"/> dependency property.</summary>
    public static readonly DependencyProperty EditorLanguageProperty = DependencyProperty.Register(
        nameof(EditorLanguage), typeof(string), typeof(CodeEditorView),
        new PropertyMetadata("text", (d, e) => ((CodeEditorView)d).ApplyLanguage()));

    /// <summary>Identifies the <see cref="EnableScriptCompletion"/> dependency property.</summary>
    public static readonly DependencyProperty EnableScriptCompletionProperty = DependencyProperty.Register(
        nameof(EnableScriptCompletion), typeof(bool), typeof(CodeEditorView),
        new PropertyMetadata(false, (d, e) => ((CodeEditorView)d).ApplyScriptCompletion()));

    private readonly List<IDisposable> _scriptProviders = [];
    private bool _resetting;

    /// <summary>Initializes the editor.</summary>
    public CodeEditorView()
    {
        InitializeComponent();
        Editor.Model.ContentChanged += OnModelContentChanged;
        Loaded += (_, _) =>
        {
            ApplyTheme();
            ApplyScriptCompletion();
        };
        ActualThemeChanged += (_, _) => ApplyTheme();
        ApplyLanguage();
    }

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

    /// <summary>Gets or sets whether typing <c>pm.</c> or <c>console.</c> offers the script API; for script editors only.</summary>
    public bool EnableScriptCompletion
    {
        get => (bool)GetValue(EnableScriptCompletionProperty);
        set => SetValue(EnableScriptCompletionProperty, value);
    }

    /// <inheritdoc/>
    string ICodeEditor.Language
    {
        get => EditorLanguage;
        set => EditorLanguage = value;
    }

    // Registered for both script languages; the editor picks the provider by its language id.
    private void ApplyScriptCompletion()
    {
        foreach (var registration in _scriptProviders)
        {
            registration.Dispose();
        }

        _scriptProviders.Clear();
        if (EnableScriptCompletion && Editor.LanguageFeatures is { } features)
        {
            _scriptProviders.Add(features.CompletionProviders.Register("javascript", new ScriptCompletionProvider(lua: false)));
            _scriptProviders.Add(features.CompletionProviders.Register("lua", new ScriptCompletionProvider(lua: true)));
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

    private void ApplyLanguage() => Editor.LanguageId = EditorLanguage switch
    {
        "json" => "json",
        "xml" => "xml",
        "javascript" => "javascript",
        "lua" => "lua",
        _ => "plaintext",
    };

    private void ApplyTheme()
    {
        var light = ActualTheme == ElementTheme.Light;
        Color C(string key) => ((SolidColorBrush)BrushMap.Themed(key, light)).Color;
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
              {{Rule("YaapicSyntaxPunctuationBrush", "punctuation", "meta.brace", "meta.structure.dictionary.json", "keyword.operator")}},
              {{Rule("YaapicSyntaxStringBrush", "string", "string.quoted.double.json")}},
              {{Rule("YaapicSyntaxKeyBrush", "support.type.property-name", "support.type.property-name.json")}},
              {{Rule("YaapicSyntaxNumberBrush", "constant.numeric")}},
              {{Rule("YaapicSyntaxBooleanBrush", "constant.language")}},
              {{Rule("YaapicSyntaxTagBrush", "entity.name.tag", "keyword", "storage")}},
              {{Rule("YaapicSyntaxAttributeBrush", "entity.other.attribute-name", "entity.name.function", "support.function")}},
              {{Rule("YaapicSyntaxCommentBrush", "comment")}}]}
            """;
        Editor.Theme = EditorTheme.Load(json, light ? "vs" : "vs-dark", "Yaapic", _ => null);
    }
}
