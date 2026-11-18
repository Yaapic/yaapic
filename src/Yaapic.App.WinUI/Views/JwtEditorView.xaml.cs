using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>JWT editor bound to a <see cref="JwtEditorViewModel"/>.</summary>
public sealed partial class JwtEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(JwtEditorViewModel), typeof(JwtEditorView),
        new PropertyMetadata(null, (d, _) => ((JwtEditorView)d).OnViewModelChanged()));

    /// <summary>Initializes the view.</summary>
    public JwtEditorView() => InitializeComponent();

    /// <summary>Gets or sets the edited JWT configuration.</summary>
    public JwtEditorViewModel? ViewModel
    {
        get => (JwtEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged()
    {
        Bindings?.Update();
        if (ViewModel is { } vm)
        {
            // Index based pickers: boxed managed enums cannot cross into XAML under Native AOT.
            AlgorithmBox.SelectedIndex = IndexOf(JwtEditorViewModel.Algorithms, vm.Algorithm);
            PlacementBox.SelectedIndex = IndexOf(JwtEditorViewModel.Placements, vm.Placement);
            ExpiresBox.Value = vm.ExpiresInSeconds;
        }
    }

    private void OnAlgorithmChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && AlgorithmBox.SelectedIndex >= 0)
        {
            ViewModel.Algorithm = JwtEditorViewModel.Algorithms[AlgorithmBox.SelectedIndex];
        }
    }

    private void OnPlacementChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && PlacementBox.SelectedIndex >= 0)
        {
            ViewModel.Placement = JwtEditorViewModel.Placements[PlacementBox.SelectedIndex];
        }
    }

    private void OnExpiresChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ViewModel is not null)
        {
            // An emptied box reports NaN: treat it as "never expires".
            ViewModel.ExpiresInSeconds = double.IsNaN(sender.Value) ? 0 : (int)Math.Clamp(sender.Value, 0, int.MaxValue);
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
