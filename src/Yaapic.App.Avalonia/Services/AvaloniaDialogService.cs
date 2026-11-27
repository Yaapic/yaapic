using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Yaapic.App.Avalonia.Views;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.App.Avalonia.Services;

/// <summary>Shows the ViewModel confirmation prompts as small modal windows over the main window.</summary>
public sealed class AvaloniaDialogService : IDialogService
{
    private readonly Func<Window?> _owner;

    /// <summary>Creates the service.</summary>
    /// <param name="owner">Returns the window that owns the dialogs, or <see langword="null"/> before it is shown.</param>
    public AvaloniaDialogService(Func<Window?> owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message) => await ShowAsync(
        title,
        message,
        false,
        (Strings.CommonContinue, true, "accent"), (Strings.CommonCancel, false, null));

    /// <inheritdoc />
    public Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName) => ShowAsync(
        Strings.DialogSaveChangesTitle(itemName),
        Strings.DialogSaveChangesBody,
        UnsavedChoice.Cancel,
        (Strings.CommonSave, UnsavedChoice.Save, "accent"), (Strings.DialogDontSave, UnsavedChoice.Discard, null), (Strings.CommonCancel, UnsavedChoice.Cancel, null));

    /// <inheritdoc />
    public async Task<bool> ConfirmDeleteAsync(string itemName) => await ShowAsync(
        Strings.DialogDeleteTitle(itemName),
        Strings.DialogDeleteBody,
        false,
        (Strings.CommonDelete, true, "danger"), (Strings.CommonCancel, false, null));

    /// <inheritdoc />
    public async Task<string?> PromptTextAsync(string title, string label, string initialText)
    {
        if (Owner() is not { } owner)
        {
            return null;
        }

        return await Dispatcher.UIThread.InvokeAsync(() => new PromptWindow(title, label, initialText).ShowDialog<string?>(owner));
    }

    /// <inheritdoc />
    public async Task<string?> PickFileAsync(string title)
    {
        if (Owner() is not { } owner)
        {
            return null;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<string?> PickFolderAsync(string title)
    {
        if (Owner() is not { } owner)
        {
            return null;
        }

        try
        {
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveFileAsync(string suggestedName, byte[] content)
    {
        if (Owner() is not { } owner)
        {
            return false;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = Path.GetExtension(suggestedName).TrimStart('.'),
            FileTypeChoices = Path.GetExtension(suggestedName) is { Length: > 1 } ext
                ? [new FilePickerFileType(ext.TrimStart('.').ToUpperInvariant()) { Patterns = ["*" + ext] }]
                : null,
        });
        if (file?.TryGetLocalPath() is not { } path)
        {
            return false;
        }

        try
        {
            await File.WriteAllBytesAsync(path, content);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task SetClipboardTextAsync(string text)
    {
        if (Owner()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private Window? Owner() => ToolWindowHost.ActiveWindow ?? _owner();

    private async Task<T> ShowAsync<T>(string title, string body, T dismissed, params (string Text, T Result, string? Class)[] buttons)
    {
        if (Owner() is not { } owner)
        {
            return dismissed;
        }

        var dialog = new Window { Title = title, Classes = { "dialog" } };
        var row = new UniformGrid { Rows = 1 };
        foreach (var (text, result, cls) in buttons)
        {
            var isDismiss = EqualityComparer<T>.Default.Equals(result, dismissed);
            var button = new Button { Content = text, IsDefault = isDismiss, IsCancel = isDismiss };
            if (cls is not null)
            {
                button.Classes.Add(cls);
            }

            button.Click += (_, _) => dialog.Close(result);
            row.Children.Add(button);
        }

        var titleText = new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap };
        titleText.Classes.Add("title");
        var bodyText = new TextBlock { Text = body };
        bodyText.Classes.Add("dialogBody");
        var commands = new Border { Child = row };
        commands.Classes.Add("dialogCommands");
        dialog.Content = new StackPanel
        {
            Children =
            {
                new StackPanel { Margin = new Thickness(24, 24, 24, 20), Spacing = 8, Children = { titleText, bodyText } },
                commands,
            },
        };
        return await Dispatcher.UIThread.InvokeAsync(() => dialog.ShowDialog<T>(owner)) ?? dismissed;
    }
}
