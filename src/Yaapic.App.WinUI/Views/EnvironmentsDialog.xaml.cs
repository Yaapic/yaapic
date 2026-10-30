using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Environments;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that creates, edits and deletes environments through an <see cref="EnvironmentsViewModel"/>.</summary>
public sealed partial class EnvironmentsDialog : ContentDialog
{
    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The environments to manage.</param>
    public EnvironmentsDialog(EnvironmentsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        DeleteButton.Flyout.Opening += (_, _) => DeletePrompt.Text = $"Delete '{ViewModel.EditedEnvironment?.Name}'?";
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EnvironmentsViewModel.EditedEnvironment))
            {
                Variables.ViewModel = ViewModel.EditedEnvironment?.Variables;
            }
        };
        Variables.ViewModel = ViewModel.EditedEnvironment?.Variables;
    }

    /// <summary>Gets the managed environments.</summary>
    public EnvironmentsViewModel ViewModel { get; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EditedEnvironment is { } item)
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
