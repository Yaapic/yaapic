using Avalonia.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>OAuth 2.0 editor bound to an <see cref="OAuth2EditorViewModel"/>.</summary>
public sealed partial class OAuth2EditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public OAuth2EditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncPickers();
    }

    private OAuth2EditorViewModel? Editor => DataContext as OAuth2EditorViewModel;

    // Index based pickers, as in AuthEditorView: the position is the one in the view model lists.
    private void SyncPickers()
    {
        if (Editor is { } vm)
        {
            GrantBox.SelectedIndex = IndexOf(OAuth2EditorViewModel.Grants, vm.Grant);
            ClientAuthBox.SelectedIndex = IndexOf(OAuth2EditorViewModel.ClientAuthentications, vm.ClientAuthentication);
            PlacementBox.SelectedIndex = IndexOf(OAuth2EditorViewModel.Placements, vm.Placement);
        }
    }

    private void OnGrantChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } vm && GrantBox.SelectedIndex >= 0)
        {
            vm.Grant = OAuth2EditorViewModel.Grants[GrantBox.SelectedIndex];
        }
    }

    private void OnClientAuthChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } vm && ClientAuthBox.SelectedIndex >= 0)
        {
            vm.ClientAuthentication = OAuth2EditorViewModel.ClientAuthentications[ClientAuthBox.SelectedIndex];
        }
    }

    private void OnPlacementChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } vm && PlacementBox.SelectedIndex >= 0)
        {
            vm.Placement = OAuth2EditorViewModel.Placements[PlacementBox.SelectedIndex];
        }
    }

    private static int IndexOf<T>(IReadOnlyList<T> items, T value)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(items[i], value))
            {
                return i;
            }
        }

        return 0;
    }
}
