using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Yaapic.ViewModels.Runner;

namespace Yaapic.App.WinUI.Views;

/// <summary>The Runner window content: the setup pane on the left, the results pane (<c>ResultsHost</c>) on the right.</summary>
public sealed partial class RunnerView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RunnerViewModel), typeof(RunnerView),
        new PropertyMetadata(null, (d, _) => ((RunnerView)d).Bindings.Update()));

    /// <summary>Initializes the view.</summary>
    public RunnerView()
    {
        InitializeComponent();
        _ = new ColumnResizer(Root, LeftColumn, Splitter);
        PaneLayout.Attach(Pane, Options, Footer);
    }

    /// <summary>Gets or sets the Runner view model.</summary>
    public RunnerViewModel? ViewModel
    {
        get => (RunnerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Gets the host the results pane is placed in.</summary>
    public Border Results => ResultsHost;

    // An emptied box reads NaN: 0 and -1 are out of range, so the validation line explains it.
    private void OnIterationsChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.Setup.Iterations = double.IsNaN(e.NewValue) ? 0 : (int)e.NewValue;
        }
    }

    private void OnDelayChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.Setup.DelayMs = double.IsNaN(e.NewValue) ? -1 : (int)e.NewValue;
        }
    }
}
