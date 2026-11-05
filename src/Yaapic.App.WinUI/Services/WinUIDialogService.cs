using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;
using Yaapic.UI.Abstractions;

namespace Yaapic.App.WinUI.Services;

/// <summary>Shows the ViewModel confirmation prompts as <see cref="ContentDialog"/>s on the main window.</summary>
public sealed class WinUIDialogService : IDialogService
{
    private readonly Func<XamlRoot?> _xamlRoot;
    private readonly Window _window;

    /// <summary>Creates the service.</summary>
    /// <param name="window">The main window, the owner of the file pickers.</param>
    /// <param name="xamlRoot">Returns the XAML root of the window content, or <see langword="null"/> before it is loaded.</param>
    public WinUIDialogService(Window window, Func<XamlRoot?> xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(xamlRoot);
        _window = window;
        _xamlRoot = xamlRoot;
    }

    /// <inheritdoc/>
    public async Task<UnsavedChoice> ConfirmUnsavedAsync(string itemName)
    {
        var result = await ShowAsync(new ContentDialog
        {
            Title = $"Save changes to '{itemName}'?",
            Content = Body("Your changes will be lost if you don't save them."),
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Don't save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            PrimaryButtonStyle = (Style)Application.Current.Resources["YaapicPrimaryButtonStyle"],
            SecondaryButtonStyle = DialogButtonStyle,
            CloseButtonStyle = DialogButtonStyle,
        });
        return result switch
        {
            ContentDialogResult.Primary => UnsavedChoice.Save,
            ContentDialogResult.Secondary => UnsavedChoice.Discard,
            _ => UnsavedChoice.Cancel,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> ConfirmDeleteAsync(string itemName) =>
        await ShowAsync(new ContentDialog
        {
            Title = $"Delete '{itemName}'?",
            Content = Body("The request file will be deleted from the workspace."),
            PrimaryButtonText = "Delete",
            PrimaryButtonStyle = (Style)Application.Current.Resources["YaapicDangerButtonStyle"],
            CloseButtonText = "Cancel",
            CloseButtonStyle = DialogButtonStyle,
            DefaultButton = ContentDialogButton.Close,
        }) == ContentDialogResult.Primary;

    /// <inheritdoc/>
    public async Task<string?> PromptTextAsync(string title, string label, string initialText)
    {
        var box = new TextBox { Header = label, Text = initialText };
        box.Loaded += (_, _) => box.SelectAll();
        var dialog = new ContentDialog
        {
            Title = title,
            Content = box,
            PrimaryButtonText = "OK",
            PrimaryButtonStyle = (Style)Application.Current.Resources["YaapicPrimaryButtonStyle"],
            CloseButtonText = "Cancel",
            CloseButtonStyle = DialogButtonStyle,
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(initialText),
        };
        box.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(box.Text);
        return await ShowAsync(dialog) == ContentDialogResult.Primary ? box.Text.Trim() : null;
    }

    /// <inheritdoc/>
    public async Task<string?> PickFileAsync(string title)
    {
        var picker = new FileOpenPicker { CommitButtonText = title };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        return (await picker.PickSingleFileAsync())?.Path;
    }

    /// <inheritdoc/>
    public async Task<bool> SaveFileAsync(string suggestedName, byte[] content)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName };
        var extension = Path.GetExtension(suggestedName);
        if (extension.Length > 1)
        {
            picker.FileTypeChoices.Add($"{extension.TrimStart('.').ToUpperInvariant()} file", [extension]);
        }

        picker.FileTypeChoices.Add("All files", ["."]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        if (await picker.PickSaveFileAsync() is not { } file)
        {
            return false;
        }

        try
        {
            await File.WriteAllBytesAsync(file.Path, content);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public Task SetClipboardTextAsync(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        return Task.CompletedTask;
    }

    private static Style DialogButtonStyle => (Style)Application.Current.Resources["YaapicDialogButtonStyle"];

    private static TextBlock Body(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Style = (Style)Application.Current.Resources["YaapicDialogBodyTextStyle"],
    };

    private async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        if (_xamlRoot() is not { } root)
        {
            return ContentDialogResult.None;
        }

        dialog.XamlRoot = root;
        DialogButtons.Normalize(dialog);
        // A dialog is hosted in a popup layer that ignores the window content theme override.
        dialog.RequestedTheme = ((FrameworkElement)root.Content).RequestedTheme;
        return await dialog.ShowAsync();
    }
}
