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

    /// <summary>Asks for a line of text. The default does nothing; platforms override it.</summary>
    /// <param name="title">The dialog title.</param>
    /// <param name="label">The label of the text box.</param>
    /// <param name="initialText">The text initially shown.</param>
    /// <returns>The entered text, or <see langword="null"/> when canceled.</returns>
    Task<string?> PromptTextAsync(string title, string label, string initialText) => Task.FromResult<string?>(null);

    /// <summary>Asks for an existing file. The default does nothing; platforms override it.</summary>
    /// <param name="title">The dialog title.</param>
    /// <returns>The chosen file path, or <see langword="null"/> when canceled.</returns>
    Task<string?> PickFileAsync(string title) => Task.FromResult<string?>(null);

    /// <summary>Asks for a destination and writes the bytes there. The default does nothing; platforms override it.</summary>
    /// <param name="suggestedName">The suggested file name.</param>
    /// <param name="content">The bytes to write.</param>
    /// <returns><see langword="true"/> when written; <see langword="false"/> when canceled or on an I/O error.</returns>
    Task<bool> SaveFileAsync(string suggestedName, byte[] content) => Task.FromResult(false);

    /// <summary>Puts text on the clipboard. The default does nothing; platforms override it.</summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>A task that completes when the text is copied.</returns>
    Task SetClipboardTextAsync(string text) => Task.CompletedTask;
}
