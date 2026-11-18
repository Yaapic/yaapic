using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Localization;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that shows the generated code of a request in the chosen language, through a <see cref="CodeGenerationViewModel"/>.</summary>
public sealed partial class CodeGenerationDialog : ContentDialog
{
    private static readonly TimeSpan CopiedDuration = TimeSpan.FromSeconds(2);

    private readonly DispatcherTimer _copiedTimer = new() { Interval = CopiedDuration };

    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The code generation view model.</param>
    public CodeGenerationDialog(CodeGenerationViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        InitializeComponent();
        Services.DialogButtons.Normalize(this);
        _copiedTimer.Tick += (_, _) => ResetCopyLabel();
        Closed += (_, _) => _copiedTimer.Stop();
    }

    /// <summary>Gets the code generation view model.</summary>
    public CodeGenerationViewModel ViewModel { get; }

    // The command runs first (Button.Command); the brief "Copied" state is purely visual, so the view model has no timer.
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CopyCommand.CanExecute(null))
        {
            return;
        }

        CopyLabel.Text = Strings.CodeGenerationCopied;
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    private void ResetCopyLabel()
    {
        _copiedTimer.Stop();
        CopyLabel.Text = Strings.CodeGenerationCopy;
    }
}
