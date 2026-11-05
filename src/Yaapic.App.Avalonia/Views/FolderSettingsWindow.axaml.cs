using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Collections;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that edits the name and description of a folder and the variables of a collection through a <see cref="FolderSettingsViewModel"/>.</summary>
public sealed partial class FolderSettingsWindow : Window
{
    /// <summary>Creates the window.</summary>
    public FolderSettingsWindow() => InitializeComponent();

    /// <summary>Caps the window height so it stays inside its owner, below the title bar, and scrolls its body instead.</summary>
    /// <param name="owner">The owner window.</param>
    internal void FitTo(Window owner) => MaxHeight = Math.Max(240, owner.Bounds.Height - 80);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
