using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yaapic.Localization;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Window that shows the generated code of a request in the chosen language, through a <see cref="CodeGenerationViewModel"/>.</summary>
public sealed partial class CodeGenerationWindow : Window
{
    private readonly DispatcherTimer _copiedTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public CodeGenerationWindow()
    {
        InitializeComponent();
        _copiedTimer.Tick += (_, _) => ResetCopyLabel();
        Closed += (_, _) => _copiedTimer.Stop();
    }

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The code generation view model.</param>
    public CodeGenerationWindow(CodeGenerationViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
    }

    // The command runs first (Button.Command); the brief "Copied" state is purely visual, so the view model has no timer.
    private void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CodeGenerationViewModel { CopyCommand: var copy } || !copy.CanExecute(null))
        {
            return;
        }

        CopyButton.Content = Strings.CodeGenerationCopied;
        _copiedTimer.Stop();
        _copiedTimer.Start();
    }

    private void ResetCopyLabel()
    {
        _copiedTimer.Stop();
        CopyButton.Content = Strings.CodeGenerationCopy;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
