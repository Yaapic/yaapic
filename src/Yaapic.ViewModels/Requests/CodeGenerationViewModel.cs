using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.CodeGeneration;
using Yaapic.Core.Http;
using Yaapic.Core.Preferences;
using Yaapic.Core.Storage;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Requests;

/// <summary>A code generation target offered to the user.</summary>
/// <param name="Id">The generator id.</param>
/// <param name="DisplayName">The name shown to the user.</param>
public sealed record CodeTarget(string Id, string DisplayName);

/// <summary>Generates source code for the current request in the chosen language.</summary>
public sealed partial class CodeGenerationViewModel : ObservableObject
{
    private const string DefaultTargetId = "curl";

    private readonly CodeGeneratorRegistry _registry;
    private readonly Func<ResolvedRequest> _resolve;
    private readonly IPreferencesStore _preferences;
    private readonly IDialogService _clipboard;
    private CodeTarget? _selectedTarget;
    private string _code = "";
    private string _editorLanguage = "plaintext";
    private string? _error;
    private string? _unresolvedWarning;

    /// <summary>Initializes a new instance of the <see cref="CodeGenerationViewModel"/> class and generates with the default target.</summary>
    /// <param name="registry">The available generators.</param>
    /// <param name="resolve">Resolves the current request; called on every generation.</param>
    /// <param name="preferences">Stores the last chosen target.</param>
    /// <param name="clipboard">Receives the copied code.</param>
    public CodeGenerationViewModel(CodeGeneratorRegistry registry, Func<ResolvedRequest> resolve, IPreferencesStore preferences, IDialogService clipboard)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(resolve);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(clipboard);
        _registry = registry;
        _resolve = resolve;
        _preferences = preferences;
        _clipboard = clipboard;
        Targets = [.. registry.All.Select(g => new CodeTarget(g.Id, g.DisplayName))];
        _selectedTarget = Pick(DefaultTargetId);
        Refresh();
    }

    /// <summary>Gets the targets, in registry order.</summary>
    public IReadOnlyList<CodeTarget> Targets { get; }

    /// <summary>Gets the warning that the generated text may contain secrets.</summary>
    public string SecretWarning => Strings.CodeGenerationSecretWarning;

    /// <summary>Gets or sets the chosen target; changing it regenerates the code and remembers the choice.</summary>
    public CodeTarget? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (SetProperty(ref _selectedTarget, value))
            {
                Refresh();
                if (value is not null)
                {
                    _ = SaveTargetAsync(value.Id);
                }
            }
        }
    }

    /// <summary>Gets the generated code; empty when nothing could be generated.</summary>
    public string Code { get => _code; private set { if (SetProperty(ref _code, value)) { CopyCommand.NotifyCanExecuteChanged(); } } }

    /// <summary>Gets the editor language id of the generated code.</summary>
    public string EditorLanguage { get => _editorLanguage; private set => SetProperty(ref _editorLanguage, value); }

    /// <summary>Gets the generation error, or <see langword="null"/>.</summary>
    public string? Error { get => _error; private set => SetProperty(ref _error, value); }

    /// <summary>Gets the warning listing the unresolved variables, or <see langword="null"/> when there are none.</summary>
    public string? UnresolvedWarning { get => _unresolvedWarning; private set => SetProperty(ref _unresolvedWarning, value); }

    /// <summary>Restores the target saved in the preferences and regenerates.</summary>
    /// <returns>A task completing when the target is restored.</returns>
    public async Task InitializeAsync()
    {
        var saved = (await _preferences.LoadAsync(CancellationToken.None)).CodegenTarget;
        if (Pick(saved) is { } target && target != _selectedTarget)
        {
            _selectedTarget = target;
            OnPropertyChanged(nameof(SelectedTarget));
            Refresh();
        }
    }

    /// <summary>Regenerates the code from the current state of the request.</summary>
    public void Refresh()
    {
        Code = "";
        Error = null;
        UnresolvedWarning = null;
        if (_selectedTarget is null || _registry.Find(_selectedTarget.Id) is not { } generator)
        {
            return;
        }

        EditorLanguage = generator.EditorLanguage;
        try
        {
            var resolved = _resolve();
            UnresolvedWarning = resolved.Unresolved.Count == 0 ? null : Strings.CodeGenerationUnresolved(string.Join(", ", resolved.Unresolved));
            Code = generator.Generate(CodeRequestFactory.From(resolved));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Error = Strings.CodeGenerationFailed(ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasCode))]
    private Task CopyAsync() => _clipboard.SetClipboardTextAsync(Code);

    private bool HasCode() => Code.Length > 0;

    private CodeTarget? Pick(string id) => Targets.FirstOrDefault(t => t.Id == id) ?? Targets.FirstOrDefault();

    private async Task SaveTargetAsync(string id)
    {
        try
        {
            var current = await _preferences.LoadAsync(CancellationToken.None);
            await _preferences.SaveAsync(current with { CodegenTarget = id }, CancellationToken.None);
        }
        catch (StorageException)
        {
            // The choice is a convenience; a read-only store only loses the memory of it.
        }
    }
}
