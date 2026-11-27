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

    private const double SplitterStep = 16;
    private bool _dragging;
    private double _lastX;

    /// <summary>Initializes the view.</summary>
    public RunnerView()
    {
        InitializeComponent();
        Splitter.SetVertical(true);
    }

    /// <summary>Gets or sets the Runner view model.</summary>
    public RunnerViewModel? ViewModel
    {
        get => (RunnerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Gets the host the results pane is placed in.</summary>
    public Border Results => ResultsHost;

    private void OnMoveUp(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is RunnerItemViewModel item)
        {
            ViewModel?.Setup.MoveUpCommand.Execute(item);
        }
    }

    private void OnMoveDown(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is RunnerItemViewModel item)
        {
            ViewModel?.Setup.MoveDownCommand.Execute(item);
        }
    }

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

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Splitter.CapturePointer(e.Pointer))
        {
            _dragging = true;
            _lastX = e.GetCurrentPoint(null).Position.X;
            Splitter.Focus(FocusState.Pointer);
        }
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            var x = e.GetCurrentPoint(null).Position.X;
            Resize(x - _lastX);
            _lastX = x;
        }
    }

    private void OnSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        Splitter.ReleasePointerCaptures();
    }

    private void OnSplitterKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left: Resize(-SplitterStep); break;
            case Windows.System.VirtualKey.Right: Resize(SplitterStep); break;
            default: return;
        }

        e.Handled = true;
    }

    private void Resize(double delta) =>
        LeftColumn.Width = new GridLength(Math.Clamp(LeftColumn.ActualWidth + delta, LeftColumn.MinWidth, LeftColumn.MaxWidth));
}
