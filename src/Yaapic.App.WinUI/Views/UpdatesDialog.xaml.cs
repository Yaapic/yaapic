using Microsoft.UI.Xaml.Controls;
using Yaapic.Core.Updates;
using Yaapic.ViewModels.Updates;

namespace Yaapic.App.WinUI.Views;

/// <summary>The About and update settings dialog: version, channel, automatic check and the state of the update.</summary>
public sealed partial class UpdatesDialog : ContentDialog
{
    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The update view model.</param>
    public UpdatesDialog(UpdateViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        Services.DialogButtons.Normalize(this);
        VersionText.Text = $"Version {viewModel.CurrentVersion}";
        ChannelBox.SelectedIndex = viewModel.Channel == AppChannel.Preview ? 1 : 0;
        ChannelBox.IsEnabled = viewModel.IsSupported;
    }

    /// <summary>Gets the update view model.</summary>
    public UpdateViewModel ViewModel { get; }

    private void OnChannelChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.Channel = ChannelBox.SelectedIndex == 1 ? AppChannel.Preview : AppChannel.Stable;
}
