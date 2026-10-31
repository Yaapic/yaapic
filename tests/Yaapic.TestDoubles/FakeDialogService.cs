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
}
