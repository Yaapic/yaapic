using CommunityToolkit.Mvvm.ComponentModel;

namespace Yaapic.ViewModels.Editing;

/// <summary>One editable row (key, value, enabled flag) of a <see cref="KeyValueEditorViewModel"/>.</summary>
public sealed partial class KeyValueRowViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _key = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _value = "";

    [ObservableProperty]
    private bool _enabled = true;

    /// <summary>Gets a value indicating whether both key and value are empty.</summary>
    public bool IsEmpty => Key.Length == 0 && Value.Length == 0;
}
