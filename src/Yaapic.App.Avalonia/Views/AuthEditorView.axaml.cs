using Avalonia.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Authentication editor bound to an <see cref="AuthEditorViewModel"/>.</summary>
public sealed partial class AuthEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public AuthEditorView() => InitializeComponent();

    private AuthEditorViewModel? Auth => DataContext as AuthEditorViewModel;

    private void OnKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Index based: the position is the one in AuthEditorViewModel.Kinds.
        if (Auth is { } auth && KindBox.SelectedIndex >= 0)
        {
            auth.Kind = AuthEditorViewModel.Kinds[KindBox.SelectedIndex];
        }
    }

    private void OnLocationChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Auth is { } auth && LocationBox.SelectedIndex >= 0)
        {
            auth.Location = AuthEditorViewModel.Locations[LocationBox.SelectedIndex];
        }
    }
}
