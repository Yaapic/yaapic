using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Yaapic.App.Avalonia.Views;
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
    public Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName) => ShowAsync(
        $"Save changes to '{itemName}'?",
        "Your changes will be lost if you don't save them.",
        UnsavedChoice.Cancel,
        ("Save", UnsavedChoice.Save), ("Don't save", UnsavedChoice.Discard), ("Cancel", UnsavedChoice.Cancel));

    /// <inheritdoc />
    public async Task<bool> ConfirmDeleteAsync(string itemName) => await ShowAsync(
        $"Delete '{itemName}'?",
        "The request file will be deleted from the workspace.",
        false,
        ("Delete", true), ("Cancel", false));

    /// <inheritdoc />
    public async Task<string?> PromptTextAsync(string title, string label, string initialText)
    {
        if (_owner() is not { } owner)
        {
            return null;
        }

        return await Dispatcher.UIThread.InvokeAsync(() => new PromptWindow(title, label, initialText).ShowDialog<string?>(owner));
    }

    /// <inheritdoc />
    public async Task<string?> PickFileAsync(string title)
    {
        if (_owner() is not { } owner)
        {
            return null;
        }

        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    /// <inheritdoc />
    public async Task<bool> SaveFileAsync(string suggestedName, byte[] content)
    {
        if (_owner() is not { } owner)
        {
            return false;
        }

        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = suggestedName });
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
        if (_owner()?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    private async Task<T> ShowAsync<T>(string title, string body, T dismissed, params (string Text, T Result)[] buttons)
    {
        if (_owner() is not { } owner)
        {
            return dismissed;
        }

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        dialog.Bind(Window.BackgroundProperty, dialog.GetResourceObservable("YaapicSurfaceElevatedBrush"));
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (var (text, result) in buttons)
        {
            var button = new Button { Content = text };
            button.Click += (_, _) => dialog.Close(result);
            row.Children.Add(button);
        }

        dialog.Content = new StackPanel
        {
            Margin = new global::Avalonia.Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = title, FontSize = 16, FontWeight = global::Avalonia.Media.FontWeight.SemiBold, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap },
                new TextBlock { Text = body, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap },
                row,
            },
        };
        return await Dispatcher.UIThread.InvokeAsync(() => dialog.ShowDialog<T>(owner)) ?? dismissed;
    }
}
