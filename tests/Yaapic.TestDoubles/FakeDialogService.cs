using Yaapic.UI.Abstractions;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IDialogService"/> with scripted answers that records its calls.</summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>Gets or sets the answer to <see cref="ConfirmUnsavedAsync"/>.</summary>
    public UnsavedChoice UnsavedAnswer { get; set; } = UnsavedChoice.Cancel;

    /// <summary>Gets or sets the answer to <see cref="ConfirmDeleteAsync"/>.</summary>
    public bool DeleteAnswer { get; set; }

    /// <summary>Gets the item names passed to <see cref="ConfirmUnsavedAsync"/>.</summary>
    public List<string> UnsavedPrompts { get; } = [];

    /// <summary>Gets the item names passed to <see cref="ConfirmDeleteAsync"/>.</summary>
    public List<string> DeletePrompts { get; } = [];

    /// <inheritdoc/>
    public Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName)
    {
        UnsavedPrompts.Add(itemName);
        return Task.FromResult(UnsavedAnswer);
    }

    /// <inheritdoc/>
    public Task<bool> ConfirmDeleteAsync(string itemName)
    {
        DeletePrompts.Add(itemName);
        return Task.FromResult(DeleteAnswer);
    }

    /// <summary>Gets the answers returned by <see cref="PromptTextAsync"/>, in order; empty means canceled.</summary>
    public Queue<string?> PromptAnswers { get; } = new();

    /// <summary>Gets the calls made to <see cref="PromptTextAsync"/>.</summary>
    public List<(string Title, string Label, string Initial)> Prompts { get; } = [];

    /// <summary>Gets or sets the answer to <see cref="PickFileAsync"/>.</summary>
    public string? PickFileAnswer { get; set; }

    /// <summary>Gets or sets the answer to <see cref="SaveFileAsync"/>.</summary>
    public bool SaveFileAnswer { get; set; }

    /// <summary>Gets the files passed to <see cref="SaveFileAsync"/>.</summary>
    public List<(string Name, byte[] Content)> SavedFiles { get; } = [];

    /// <summary>Gets the texts passed to <see cref="SetClipboardTextAsync"/>.</summary>
    public List<string> ClipboardTexts { get; } = [];

    /// <inheritdoc/>
    public Task<string?> PromptTextAsync(string title, string label, string initialText)
    {
        Prompts.Add((title, label, initialText));
        return Task.FromResult(PromptAnswers.TryDequeue(out var answer) ? answer : null);
    }

    /// <inheritdoc/>
    public Task<string?> PickFileAsync(string title) => Task.FromResult(PickFileAnswer);

    /// <inheritdoc/>
    public Task<bool> SaveFileAsync(string suggestedName, byte[] content)
    {
        SavedFiles.Add((suggestedName, content));
        return Task.FromResult(SaveFileAnswer);
    }

    /// <inheritdoc/>
    public Task SetClipboardTextAsync(string text)
    {
        ClipboardTexts.Add(text);
        return Task.CompletedTask;
    }
}
