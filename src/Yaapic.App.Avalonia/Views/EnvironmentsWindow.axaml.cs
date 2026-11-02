using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.ViewModels.Environments;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that creates, edits and deletes environments through an <see cref="EnvironmentsViewModel"/>.</summary>
public sealed partial class EnvironmentsWindow : Window
{
    /// <summary>Creates the window.</summary>
    public EnvironmentsWindow()
    {
        InitializeComponent();
        ((Flyout)DeleteButton.Flyout!).Opened += (_, _) => DeletePrompt.Text = $"Delete '{ViewModel?.EditedEnvironment?.Name}'?";
    }

    private EnvironmentsViewModel? ViewModel => DataContext as EnvironmentsViewModel;

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
