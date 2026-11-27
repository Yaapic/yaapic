using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Mocks;

namespace Yaapic.App.WinUI.Views;

/// <summary>The Mock servers window content: server list, server and route editor, request log.</summary>
public sealed partial class MockServersView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(MockServersViewModel), typeof(MockServersView),
        new PropertyMetadata(null, (d, e) => ((MockServersView)d).OnViewModelChanged((MockServersViewModel?)e.OldValue)));

    private MockRouteViewModel? _route;
    private bool _syncing;

    /// <summary>Initializes the view.</summary>
    public MockServersView() => InitializeComponent();

    /// <summary>Gets or sets the Mock servers view model.</summary>
    public MockServersViewModel? ViewModel
    {
        get => (MockServersViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged(MockServersViewModel? old)
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
        FollowRoute();
        SyncServerSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MockServersViewModel.SelectedRoute):
                FollowRoute();
                break;
            case nameof(MockServersViewModel.SelectedServer):
                SyncServerSelection();
                break;
        }
    }

    // The body language follows the Content-Type header of the selected route.
    private void FollowRoute()
    {
        if (_route is not null)
        {
            _route.Changed -= OnRouteChanged;
        }

        _route = ViewModel?.SelectedRoute;
        if (_route is not null)
        {
            _route.Changed += OnRouteChanged;
        }

        OnRouteChanged(null, EventArgs.Empty);
    }

    private void OnRouteChanged(object? sender, EventArgs e) =>
        BodyEditor.EditorLanguage = _route is not null && _route.Headers.ToEntries().Any(h =>
            h.Enabled && h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
            && h.Value.Contains("json", StringComparison.OrdinalIgnoreCase))
            ? "json"
            : "text";

    // Keeps the list selection in step with the view model (a canceled switch, a deleted server, a new one).
    private void SyncServerSelection()
    {
        _syncing = true;
        try
        {
            ServerList.SelectedItem = ViewModel?.SelectedServer;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void OnServerSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || ViewModel is not { } vm)
        {
            return;
        }

        // Only a user click reaches here; the view model asks about unsaved edits and may refuse.
        if (!await vm.SelectServerAsync((MockServerItemViewModel?)ServerList.SelectedItem))
        {
            SyncServerSelection();
        }
    }

    // An emptied box reads NaN: the out-of-range value is reported by the validation lines on save.
    private static int Read(double value, int empty) => double.IsNaN(value) ? empty : (int)value;

    private void OnPortChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.Port = Read(e.NewValue, 0);
        }
    }

    private void OnStatusChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (_route is not null)
        {
            _route.Status = Read(e.NewValue, 0);
        }
    }

    private void OnDelayChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (_route is not null)
        {
            _route.DelayMs = Read(e.NewValue, -1);
        }
    }
}
