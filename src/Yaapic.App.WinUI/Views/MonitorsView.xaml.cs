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
        DispatcherQueue.TryEnqueue(SyncChoices);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonitorsViewModel.SelectedMonitor))
        {
            SyncSelection();
        }

        // The choice lists are copied into new collections by the bindings (which clears a combo box selection), so the
        // folder and environment are re-selected once those bindings have run.
        if (e.PropertyName is nameof(MonitorsViewModel.SelectedMonitor) or nameof(MonitorsViewModel.SelectedFolder)
            or nameof(MonitorsViewModel.SelectedEnvironment) or nameof(MonitorsViewModel.FolderChoices)
            or nameof(MonitorsViewModel.EnvironmentChoices))
        {
            DispatcherQueue.TryEnqueue(SyncChoices);
        }
    }

    private void SyncChoices()
    {
        _syncing = true;
        try
        {
            FolderCombo.SelectedItem = ViewModel?.SelectedFolder;
            EnvironmentCombo.SelectedItem = ViewModel?.SelectedEnvironment;
        }
        finally
        {
            _syncing = false;
        }
    }

    // Only a user choice reaches the view model; the selection a binding clears is ignored.
    private void OnChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ViewModel is not { } vm)
        {
            return;
        }

        if (ReferenceEquals(sender, FolderCombo) && FolderCombo.SelectedItem is MonitorFolderChoice folder)
        {
            vm.SelectedFolder = folder;
        }
        else if (ReferenceEquals(sender, EnvironmentCombo) && EnvironmentCombo.SelectedItem is MonitorEnvironmentChoice environment)
        {
            vm.SelectedEnvironment = environment;
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
