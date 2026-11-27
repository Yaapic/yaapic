using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Core.LoadTesting;
using Yaapic.ViewModels.LoadTesting;

namespace Yaapic.App.WinUI.Views;

/// <summary>The Load test window content: configuration on the left, live metrics, charts and results on the right.</summary>
public sealed partial class LoadTestView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(LoadTestViewModel), typeof(LoadTestView),
        new PropertyMetadata(null, (d, _) => ((LoadTestView)d).Bindings.Update()));

    /// <summary>Initializes the view.</summary>
    public LoadTestView() => InitializeComponent();

    /// <summary>Gets or sets the Load test view model.</summary>
    public LoadTestViewModel? ViewModel
    {
        get => (LoadTestViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    // An emptied box reads NaN: the empty value is out of range, so the validation line explains it.
    private static int Read(double value, int empty) => double.IsNaN(value) ? empty : (int)value;

    private void OnUsersChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.VirtualUsers = Read(e.NewValue, 0);
        }
    }

    private void OnDurationChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.DurationSeconds = Read(e.NewValue, 0);
        }
    }

    private void OnRampUpChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.RampUpSeconds = Read(e.NewValue, 0);
        }
    }

    private void OnThinkChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ThinkTimeMs = Read(e.NewValue, -1);
        }
    }

    private void OnProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is { } vm && ((ComboBox)sender).SelectedIndex is >= 0 and var index)
        {
            vm.Profile = index == 1 ? LoadProfile.RampUp : LoadProfile.Fixed;
        }
    }
}
