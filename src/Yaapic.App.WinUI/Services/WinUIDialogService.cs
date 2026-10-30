using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.UI.Abstractions;

namespace Yaapic.App.WinUI.Services;

/// <summary>Shows the ViewModel confirmation prompts as <see cref="ContentDialog"/>s on the main window.</summary>
public sealed class WinUIDialogService : IDialogService
{
    private readonly Func<XamlRoot?> _xamlRoot;

    /// <summary>Creates the service.</summary>
    /// <param name="xamlRoot">Returns the XAML root of the window content, or <see langword="null"/> before it is loaded.</param>
    public WinUIDialogService(Func<XamlRoot?> xamlRoot)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
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
            PrimaryButtonStyle = (Style)Application.Current.Resources["AccentButtonStyle"],
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
        // A dialog is hosted in a popup layer that ignores the window content theme override.
        dialog.RequestedTheme = ((FrameworkElement)root.Content).RequestedTheme;
        return await dialog.ShowAsync();
    }
}
