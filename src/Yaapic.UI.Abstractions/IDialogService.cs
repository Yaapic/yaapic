namespace Yaapic.UI.Abstractions;

/// <summary>UI-neutral port for the confirmation dialogs the ViewModels need.</summary>
public interface IDialogService
{
    /// <summary>Asks what to do with unsaved changes.</summary>
    /// <param name="itemName">The name of the item with unsaved changes.</param>
    /// <returns>The choice made by the user.</returns>
    Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName);

    /// <summary>Asks whether an item may be deleted.</summary>
    /// <param name="itemName">The name of the item to delete.</param>
    /// <returns><see langword="true"/> when the deletion is confirmed.</returns>
    Task<bool> ConfirmDeleteAsync(string itemName);
}
