using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Yaapic.ViewModels.Tools;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that decodes a pasted JWT through a <see cref="JwtDecoderViewModel"/>; the signature is never verified.</summary>
public sealed partial class JwtDecoderDialog : ContentDialog
{
    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The decoder view model.</param>
    public JwtDecoderDialog(JwtDecoderViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        Services.DialogButtons.Normalize(this);
    }

    /// <summary>Gets the decoder view model.</summary>
    public JwtDecoderViewModel ViewModel { get; }

    private async void OnPasteClick(object sender, RoutedEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (content.Contains(StandardDataFormats.Text))
        {
            ViewModel.Input = (await content.GetTextAsync()).Trim();
        }
    }
}
