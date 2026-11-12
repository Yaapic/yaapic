using Yaapic.Localization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Environments;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that creates, edits and deletes environments through an <see cref="EnvironmentsViewModel"/>.</summary>
public sealed partial class EnvironmentsDialog : ContentDialog
{
    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The environments to manage.</param>
    /// <param name="globals">The global variables to edit.</param>
    public EnvironmentsDialog(EnvironmentsViewModel viewModel, GlobalsViewModel globals)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(globals);
        ViewModel = viewModel;
        Globals = globals;
        InitializeComponent();
        Services.DialogButtons.Normalize(this, new Thickness(0, 10, 16, 10));
        DeleteButton.Flyout.Opening += (_, _) => DeletePrompt.Text = Strings.DialogDeleteTitle(ViewModel.EditedEnvironment?.Name ?? "");
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EnvironmentsViewModel.EditedEnvironment))
            {
                Variables.ViewModel = ViewModel.EditedEnvironment?.Variables;
            }
        };
        Variables.ViewModel = ViewModel.EditedEnvironment?.Variables;
        GlobalVariables.ViewModel = globals.Variables;
        Loaded += (_, _) =>
        {
            Fit();
            XamlRoot.Changed += (_, _) => Fit();
        };
    }

    /// <summary>Gets the managed environments.</summary>
    public EnvironmentsViewModel ViewModel { get; }

    /// <summary>Gets the edited global variables.</summary>
    public GlobalsViewModel Globals { get; }

    // Keeps the dialog clear of the title bar (the system caption buttons draw above any popup) when the window is small.
    private void Fit()
    {
        var size = XamlRoot.Size;
        Body.Width = Math.Clamp(size.Width - 64, 320, 960);
        Body.Height = Math.Clamp(size.Height - 2 * 56 - 52, 96, 560);
    }

    private void OnSelectGlobals(object sender, RoutedEventArgs e) => ViewModel.IsEditingGlobals = true;

    // The footer Save keeps the dialog open and saves whichever pane is shown.
    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        if (ViewModel.IsEditingGlobals)
        {
            Globals.SaveCommand.Execute(null);
        }
        else if (ViewModel.EditedEnvironment is { } item)
        {
            ViewModel.SaveCommand.Execute(item);
        }
    }

    private async void OnConfirmDelete(object sender, RoutedEventArgs e)
    {
        DeleteButton.Flyout.Hide();
        if (ViewModel.EditedEnvironment is { } item)
        {
            await ViewModel.DeleteConfirmedAsync(item);
        }
    }
}
