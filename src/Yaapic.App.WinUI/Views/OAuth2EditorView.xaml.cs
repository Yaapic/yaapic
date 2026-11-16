using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>OAuth 2.0 editor bound to an <see cref="OAuth2EditorViewModel"/>.</summary>
public sealed partial class OAuth2EditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(OAuth2EditorViewModel), typeof(OAuth2EditorView),
        new PropertyMetadata(null, (d, _) => ((OAuth2EditorView)d).OnViewModelChanged()));

    /// <summary>Initializes the view.</summary>
    public OAuth2EditorView() => InitializeComponent();

    /// <summary>Gets or sets the edited OAuth 2.0 configuration.</summary>
    public OAuth2EditorViewModel? ViewModel
    {
        get => (OAuth2EditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged()
    {
        Bindings?.Update();
        if (ViewModel is { } vm)
        {
            // Index based pickers: boxed managed enums cannot cross into XAML under Native AOT.
            GrantBox.SelectedIndex = IndexOf(OAuth2EditorViewModel.Grants, vm.Grant);
            ClientAuthBox.SelectedIndex = IndexOf(OAuth2EditorViewModel.ClientAuthentications, vm.ClientAuthentication);
            PlacementBox.SelectedIndex = IndexOf(OAuth2EditorViewModel.Placements, vm.Placement);
        }
    }

    private void OnGrantChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && GrantBox.SelectedIndex >= 0)
        {
            ViewModel.Grant = OAuth2EditorViewModel.Grants[GrantBox.SelectedIndex];
        }
    }

    private void OnClientAuthChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && ClientAuthBox.SelectedIndex >= 0)
        {
            ViewModel.ClientAuthentication = OAuth2EditorViewModel.ClientAuthentications[ClientAuthBox.SelectedIndex];
        }
    }

    private void OnPlacementChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && PlacementBox.SelectedIndex >= 0)
        {
            ViewModel.Placement = OAuth2EditorViewModel.Placements[PlacementBox.SelectedIndex];
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
