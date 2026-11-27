using Avalonia.Controls;
using Yaapic.ViewModels.Monitors;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The Monitors window content: monitor list, editor, run history.</summary>
public sealed partial class MonitorsView : UserControl
{
    private MonitorsViewModel? _vm;
    private bool _syncing;

    /// <summary>Initializes the view.</summary>
    public MonitorsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => OnViewModelChanged();
    }

    private void OnViewModelChanged()
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _vm = DataContext as MonitorsViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

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
            MonitorList.SelectedItem = _vm?.SelectedMonitor;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void OnMonitorSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _vm is not { } vm)
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
