using Avalonia.Controls;
using Yaapic.App.Avalonia.Presentation;
using Yaapic.Core.LoadTesting;
using Yaapic.Core.Preferences;
using Yaapic.ViewModels.LoadTesting;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The Load test window content: configuration on the left, live metrics, charts and results on the right.</summary>
public sealed partial class LoadTestView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public LoadTestView()
    {
        InitializeComponent();
        PaneResizer.Attach(Root.ColumnDefinitions[0], ListSplitter, PaneId.LoadTestList, ((App)global::Avalonia.Application.Current!).Panes);
    }

    // An emptied box reads null: the empty value is out of range, so the validation line explains it.
    private static int Read(decimal? value, int empty) => value is { } v ? (int)v : empty;

    private void OnUsersChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is LoadTestViewModel vm)
        {
            vm.VirtualUsers = Read(e.NewValue, 0);
        }
    }

    private void OnDurationChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is LoadTestViewModel vm)
        {
            vm.DurationSeconds = Read(e.NewValue, 0);
        }
    }

    private void OnRampUpChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is LoadTestViewModel vm)
        {
            vm.RampUpSeconds = Read(e.NewValue, 0);
        }
    }

    private void OnThinkChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is LoadTestViewModel vm)
        {
            vm.ThinkTimeMs = Read(e.NewValue, -1);
        }
    }

    private void OnProfileChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is LoadTestViewModel vm && sender is ComboBox { SelectedIndex: >= 0 and var index })
        {
            vm.Profile = index == 1 ? LoadProfile.RampUp : LoadProfile.Fixed;
        }
    }
}
