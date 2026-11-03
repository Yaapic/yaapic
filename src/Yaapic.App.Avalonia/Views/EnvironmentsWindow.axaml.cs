using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Environments;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that creates, edits, duplicates and deletes environments and edits the globals through the <see cref="ShellViewModel"/>.</summary>
public sealed partial class EnvironmentsWindow : Window
{
    /// <summary>Creates the window.</summary>
    public EnvironmentsWindow()
    {
        InitializeComponent();
        ((Flyout)DeleteButton.Flyout!).Opened += (_, _) => DeletePrompt.Text = $"Delete '{ViewModel?.EditedEnvironment?.Name}'?";
    }

    private EnvironmentsViewModel? ViewModel => (DataContext as ShellViewModel)?.Environments;

    private void OnSelectGlobals(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.IsEditingGlobals = true;
        }
    }

    private void OnDuplicate(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { EditedEnvironment: { } item } vm)
        {
            vm.DuplicateCommand.Execute(item);
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { EditedEnvironment: { } item } vm)
        {
            vm.SaveCommand.Execute(item);
        }
    }

    private async void OnConfirmDelete(object? sender, RoutedEventArgs e)
    {
        DeleteButton.Flyout!.Hide();
        if (ViewModel is { EditedEnvironment: { } item } vm)
        {
            await vm.DeleteConfirmedAsync(item);
        }
    }
}
