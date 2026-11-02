using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that asks for one non-empty line of text and closes with the trimmed text, or <see langword="null"/>.</summary>
public sealed partial class PromptWindow : Window
{
    /// <summary>Creates the window.</summary>
    public PromptWindow()
    {
        InitializeComponent();
        Input.TextChanged += (_, _) => OkButton.IsEnabled = !string.IsNullOrWhiteSpace(Input.Text);
        Opened += (_, _) =>
        {
            Input.Focus();
            Input.SelectAll();
        };
    }

    /// <summary>Creates the window with its title, label and initial text.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="label">The label above the text box.</param>
    /// <param name="initialText">The text initially shown.</param>
    public PromptWindow(string title, string label, string initialText) : this()
    {
        Title = title;
        LabelText.Text = label;
        Input.Text = initialText;
        OkButton.IsEnabled = !string.IsNullOrWhiteSpace(initialText);
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(Input.Text?.Trim());

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
