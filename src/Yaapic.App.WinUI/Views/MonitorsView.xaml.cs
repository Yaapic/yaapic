using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Monitors;

namespace Yaapic.App.WinUI.Views;

/// <summary>The Monitors window content: monitor list, editor, run history.</summary>
public sealed partial class MonitorsView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MonitorsViewModel), typeof(MonitorsView),
        new PropertyMetadata(null, (d, e) => ((MonitorsView)d).OnViewModelChanged((MonitorsViewModel?)e.OldValue)));

    private bool _syncing;

    /// <summary>Initializes the view.</summary>
    public MonitorsView() => InitializeComponent();

    /// <summary>Gets or sets the Monitors view model.</summary>
    public MonitorsViewModel? ViewModel
    {
        get => (MonitorsViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged(MonitorsViewModel? old)
    {
        if (old is not null)
        {
            old.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (ViewModel is { } vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        Bindings.Update();
        SyncSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorsViewModel.SelectedMonitor))
        {
            SyncSelection();
        }
    }

    // Keeps the list selection in step with the view model (a canceled switch, a deleted monitor, a new one).
    private void SyncSelection()
    {
        _syncing = true;
        try
        {
            MonitorList.SelectedItem = ViewModel?.SelectedMonitor;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void OnMonitorSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ViewModel is not { } vm)
        {
            return;
        }

        // Only a user click reaches here; the view model asks about unsaved edits and may refuse.
        if (!await vm.SelectMonitorAsync((MonitorItemViewModel?)MonitorList.SelectedItem))
        {
            SyncSelection();
        }
    }
}
