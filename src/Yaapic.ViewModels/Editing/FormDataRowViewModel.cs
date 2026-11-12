using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Editing;

/// <summary>One editable row (key, text or file value, enabled flag) of a <see cref="FormDataEditorViewModel"/>.</summary>
public sealed partial class FormDataRowViewModel : ObservableObject
{
    private readonly IDialogService? _dialogs;

    /// <summary>Initializes a row.</summary>
    /// <param name="dialogs">The dialog service used to pick a file; <see langword="null"/> disables picking.</param>
    public FormDataRowViewModel(IDialogService? dialogs = null) => _dialogs = dialogs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _key = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _value = "";

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFile))]
    private FormFieldKind _kind;

    /// <summary>Gets a value indicating whether the value is a file path.</summary>
    public bool IsFile => Kind == FormFieldKind.File;

    /// <summary>Gets a value indicating whether both key and value are empty.</summary>
    public bool IsEmpty => Key.Length == 0 && Value.Length == 0;

    /// <summary>Asks the user for a file and, when one is chosen, stores its path as the value.</summary>
    /// <returns>A task that completes when the dialog closes.</returns>
    [RelayCommand]
    public async Task PickFileAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var path = await _dialogs.PickFileAsync(Strings.CommonSelectFile);
        if (path is not null)
        {
            Value = path;
        }
    }
}
