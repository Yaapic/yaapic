using Avalonia.Controls;
using Yaapic.App.Avalonia.Presentation;
using Yaapic.Core.Preferences;
using Yaapic.ViewModels.Runner;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The Runner window content: the setup pane on the left, the results pane (<c>ResultsHost</c>) on the right.</summary>
public sealed partial class RunnerView : UserControl
{
    /// <summary>Initializes the view.</summary>
    public RunnerView()
    {
        InitializeComponent();
        PaneResizer.Attach(Root.ColumnDefinitions[0], ListSplitter, PaneId.RunnerList, ((App)global::Avalonia.Application.Current!).Panes);
    }

    /// <summary>Gets the host the results pane is placed in.</summary>
    public Border Results => ResultsHost;

    // An emptied box reads null: 0 and -1 are out of range, so the validation line explains it.
    private void OnIterationsChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is RunnerViewModel vm)
        {
            vm.Setup.Iterations = e.NewValue is { } v ? (int)v : 0;
        }
    }

    private void OnDelayChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (DataContext is RunnerViewModel vm)
        {
            vm.Setup.DelayMs = e.NewValue is { } v ? (int)v : -1;
        }
    }
}
