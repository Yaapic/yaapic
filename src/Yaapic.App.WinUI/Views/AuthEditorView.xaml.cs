using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>Authentication editor bound to an <see cref="AuthEditorViewModel"/>.</summary>
public sealed partial class AuthEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(AuthEditorViewModel), typeof(AuthEditorView),
        new PropertyMetadata(null, (d, _) => ((AuthEditorView)d).OnViewModelChanged()));

    /// <summary>Initializes the view.</summary>
    public AuthEditorView() => InitializeComponent();

    /// <summary>Gets or sets the edited authentication.</summary>
    public AuthEditorViewModel? ViewModel
    {
        get => (AuthEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged()
    {
        Bindings?.Update();
        if (ViewModel is { } vm)
        {
            // The pickers are index based: boxed managed enums cannot cross into XAML under Native AOT.
            KindBox.SelectedIndex = IndexOf(AuthEditorViewModel.Kinds, vm.Kind);
            LocationBox.SelectedIndex = IndexOf(AuthEditorViewModel.Locations, vm.Location);
        }
    }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && KindBox.SelectedIndex >= 0)
        {
            ViewModel.Kind = AuthEditorViewModel.Kinds[KindBox.SelectedIndex];
        }
    }

    private void OnLocationChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && LocationBox.SelectedIndex >= 0)
        {
            ViewModel.Location = AuthEditorViewModel.Locations[LocationBox.SelectedIndex];
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
