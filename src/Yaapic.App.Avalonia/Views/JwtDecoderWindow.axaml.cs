using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Tools;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that decodes a pasted JWT through a <see cref="JwtDecoderViewModel"/>; the signature is never verified.</summary>
public sealed partial class JwtDecoderWindow : Window
{
    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public JwtDecoderWindow() => InitializeComponent();

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The decoder view model.</param>
    public JwtDecoderWindow(JwtDecoderViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    private async void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JwtDecoderViewModel vm && Clipboard is { } clipboard && await clipboard.TryGetTextAsync() is { } text)
        {
            vm.Input = text.Trim();
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
