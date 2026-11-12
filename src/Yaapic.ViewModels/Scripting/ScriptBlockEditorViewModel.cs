using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Scripting;

/// <summary>Editable state of one script block, convertible to and from <see cref="ScriptBlock"/>.</summary>
public sealed partial class ScriptBlockEditorViewModel : ObservableObject
{
    private readonly ScriptPhase _phase;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Placeholder))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ScriptLanguage _language;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _source;

    /// <summary>Initializes a new instance of the <see cref="ScriptBlockEditorViewModel"/> class.</summary>
    /// <param name="phase">The phase the block runs in; it selects the template.</param>
    public ScriptBlockEditorViewModel(ScriptPhase phase)
    {
        _phase = phase;
        _source = TemplateFor(_language, phase);
    }

    /// <summary>Raised on every language or source change; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the languages offered by the editor.</summary>
    public static IReadOnlyList<ScriptLanguageChoice> Languages { get; } = [
        new(ScriptLanguage.JavaScript, "JavaScript"),
        new(ScriptLanguage.Lua, "Lua"),
    ];

    /// <summary>Gets a value indicating whether the block holds no script: blank, or still the untouched template.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Source) || Source == Placeholder;

    /// <summary>
    /// Gets the commented template shown as the editor text while the block is empty, for the current language and phase.
    /// It is never persisted and never counts as a script.
    /// </summary>
    public string Placeholder => TemplateFor(Language, _phase);

    /// <summary>Builds the domain block from the current state; an empty or template-only source yields an empty source.</summary>
    /// <returns>The block.</returns>
    public ScriptBlock ToBlock() => new(Language, Source.Length == 0 || Source == Placeholder ? "" : Source);

    /// <summary>Replaces the state with <paramref name="block"/> without raising <see cref="Changed"/>.</summary>
    /// <param name="block">The block to show.</param>
    public void Load(ScriptBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _loading = true;
        try
        {
            Language = block.Language;
            Source = block.Source.Length == 0 ? Placeholder : block.Source;
        }
        finally
        {
            _loading = false;
        }
    }

    private static string TemplateFor(ScriptLanguage language, ScriptPhase phase) => (language, phase) switch
    {
        (ScriptLanguage.Lua, ScriptPhase.PreRequest) =>
            "-- " + Strings.ScriptTemplatePreRequest + "\n-- pm.environment.set(\"requestId\", pm.variables.replace_in(\"{{$guid}}\"))",
        (ScriptLanguage.Lua, _) =>
            "-- " + Strings.ScriptTemplatePostResponse + "\n-- pm.test(\"Status is 200\", function() pm.response.to.have.status(200) end)",
        (_, ScriptPhase.PreRequest) =>
            "// " + Strings.ScriptTemplatePreRequest + "\n// pm.environment.set(\"requestId\", pm.variables.replaceIn(\"{{$guid}}\"));",
        _ =>
            "// " + Strings.ScriptTemplatePostResponse + "\n// pm.test(\"Status is 200\", () => pm.response.to.have.status(200));",
    };

    // An untouched (blank or template) source follows the language, so the editor shows the new language's template.
    partial void OnLanguageChanged(ScriptLanguage oldValue, ScriptLanguage newValue)
    {
        if (Source.Length == 0 || Source == TemplateFor(oldValue, _phase))
        {
            Source = Placeholder;
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_loading && e.PropertyName is nameof(Language) or nameof(Source))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
