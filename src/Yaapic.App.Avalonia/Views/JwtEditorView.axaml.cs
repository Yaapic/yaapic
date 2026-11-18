using Avalonia.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>JWT editor bound to a <see cref="JwtEditorViewModel"/>.</summary>
public sealed partial class JwtEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public JwtEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncPickers();
    }

    private JwtEditorViewModel? Editor => DataContext as JwtEditorViewModel;

    // Index based pickers: item order matches JwtEditorViewModel.Algorithms and JwtEditorViewModel.Placements.
    private void SyncPickers()
    {
        if (Editor is { } vm)
        {
            AlgorithmBox.SelectedIndex = IndexOf(JwtEditorViewModel.Algorithms, vm.Algorithm);
            PlacementBox.SelectedIndex = IndexOf(JwtEditorViewModel.Placements, vm.Placement);
        }
    }

    private void OnAlgorithmChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } vm && AlgorithmBox.SelectedIndex >= 0)
        {
            vm.Algorithm = JwtEditorViewModel.Algorithms[AlgorithmBox.SelectedIndex];
        }
    }

    private void OnPlacementChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } vm && PlacementBox.SelectedIndex >= 0)
        {
            vm.Placement = JwtEditorViewModel.Placements[PlacementBox.SelectedIndex];
        }
    }

    private void OnExpiresChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (Editor is { } vm)
        {
            vm.ExpiresInSeconds = (int)(e.NewValue ?? 0);
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
