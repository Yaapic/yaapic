using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Scripting;

namespace Yaapic.ViewModels.Scripting;

/// <summary>Editable state of one script block, convertible to and from <see cref="ScriptBlock"/>.</summary>
public sealed partial class ScriptBlockEditorViewModel : ObservableObject
{
    private readonly ScriptPhase _phase;
    private bool _loading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Placeholder))]
    private ScriptLanguage _language;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _source = "";

    /// <summary>Initializes a new instance of the <see cref="ScriptBlockEditorViewModel"/> class.</summary>
    /// <param name="phase">The phase the block runs in; it selects the placeholder.</param>
    public ScriptBlockEditorViewModel(ScriptPhase phase) => _phase = phase;

    /// <summary>Raised on every language or source change; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the languages offered by the editor.</summary>
    public static IReadOnlyList<ScriptLanguageChoice> Languages { get; } = [
        new(ScriptLanguage.JavaScript, "JavaScript"),
        new(ScriptLanguage.Lua, "Lua"),
    ];

    /// <summary>Gets a value indicating whether the source is empty or whitespace.</summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Source);

    /// <summary>Gets the hint shown while the block is empty, for the current language and phase.</summary>
    public string Placeholder => (Language, _phase) switch
    {
        (ScriptLanguage.Lua, ScriptPhase.PreRequest) =>
            "-- Runs before the request is sent.\n-- pm.environment.set(\"requestId\", pm.variables.replace_in(\"{{$guid}}\"))",
        (ScriptLanguage.Lua, _) =>
            "-- Runs after the response arrives.\n-- pm.test(\"Status is 200\", function() pm.response.to.have.status(200) end)",
        (_, ScriptPhase.PreRequest) =>
            "// Runs before the request is sent.\n// pm.environment.set(\"requestId\", pm.variables.replaceIn(\"{{$guid}}\"));",
        _ =>
            "// Runs after the response arrives.\n// pm.test(\"Status is 200\", () => pm.response.to.have.status(200));",
    };

    /// <summary>Builds the domain block from the current state.</summary>
    /// <returns>The block.</returns>
    public ScriptBlock ToBlock() => new(Language, Source);

    /// <summary>Replaces the state with <paramref name="block"/> without raising <see cref="Changed"/>.</summary>
    /// <param name="block">The block to show.</param>
    public void Load(ScriptBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        _loading = true;
        try
        {
            Language = block.Language;
            Source = block.Source;
        }
        finally
        {
            _loading = false;
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
