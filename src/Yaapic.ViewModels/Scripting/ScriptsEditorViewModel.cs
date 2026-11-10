using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Scripting;

namespace Yaapic.ViewModels.Scripting;

/// <summary>Editable pre-request and post-response scripts of a request, convertible to and from <see cref="ScriptSet"/>.</summary>
public sealed partial class ScriptsEditorViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Selected))]
    private ScriptPhase _selectedPhase = ScriptPhase.PreRequest;

    /// <summary>Initializes a new instance of the <see cref="ScriptsEditorViewModel"/> class.</summary>
    public ScriptsEditorViewModel()
    {
        PreRequest = new ScriptBlockEditorViewModel(ScriptPhase.PreRequest);
        PostResponse = new ScriptBlockEditorViewModel(ScriptPhase.PostResponse);
        PreRequest.Changed += (_, _) => OnBlockChanged();
        PostResponse.Changed += (_, _) => OnBlockChanged();
    }

    /// <summary>Raised when either block changes; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the pre-request script editor.</summary>
    public ScriptBlockEditorViewModel PreRequest { get; }

    /// <summary>Gets the post-response script editor.</summary>
    public ScriptBlockEditorViewModel PostResponse { get; }

    /// <summary>Gets the editor of <see cref="SelectedPhase"/>.</summary>
    public ScriptBlockEditorViewModel Selected => SelectedPhase == ScriptPhase.PreRequest ? PreRequest : PostResponse;

    /// <summary>Gets the number of blocks that hold a script (0 to 2).</summary>
    public int FilledCount => (PreRequest.IsEmpty ? 0 : 1) + (PostResponse.IsEmpty ? 0 : 1);

    /// <summary>Gets the tab badge: empty when no script is set, otherwise the number of filled blocks.</summary>
    public string BadgeText => FilledCount == 0 ? "" : FilledCount.ToString(CultureInfo.InvariantCulture);

    /// <summary>Builds the domain set from the current state.</summary>
    /// <returns>The set.</returns>
    public ScriptSet ToSet() => new(PreRequest.ToBlock(), PostResponse.ToBlock());

    /// <summary>Replaces the state with <paramref name="set"/> without raising <see cref="Changed"/>.</summary>
    /// <param name="set">The scripts to show.</param>
    public void Load(ScriptSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        PreRequest.Load(set.PreRequest);
        PostResponse.Load(set.PostResponse);
        NotifyBadge();
    }

    private void NotifyBadge()
    {
        OnPropertyChanged(nameof(FilledCount));
        OnPropertyChanged(nameof(BadgeText));
    }

    private void OnBlockChanged()
    {
        NotifyBadge();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
