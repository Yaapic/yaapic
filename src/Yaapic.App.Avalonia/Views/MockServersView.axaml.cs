using Avalonia.Controls;
using Yaapic.ViewModels.Mocks;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The Mock servers window content: server list, server and route editor, request log.</summary>
public sealed partial class MockServersView : UserControl
{
    private MockServersViewModel? _vm;
    private MockRouteViewModel? _route;
    private bool _syncing;

    /// <summary>Initializes the view.</summary>
    public MockServersView()
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

        _vm = DataContext as MockServersViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

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

        _route = _vm?.SelectedRoute;
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
            ServerList.SelectedItem = _vm?.SelectedServer;
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void OnServerSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing || _vm is not { } vm)
        {
            return;
        }

        // Only a user click reaches here; the view model asks about unsaved edits and may refuse.
        if (!await vm.SelectServerAsync((MockServerItemViewModel?)ServerList.SelectedItem))
        {
            SyncServerSelection();
        }
    }

    // An emptied box reads null: the out-of-range value is reported by the validation lines on save.
    private static int Read(decimal? value, int empty) => value is { } v ? (int)v : empty;

    private void OnPortChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_vm is { } vm)
        {
            vm.Port = Read(e.NewValue, 0);
        }
    }

    private void OnStatusChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_route is not null)
        {
            _route.Status = Read(e.NewValue, 0);
        }
    }

    private void OnDelayChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_route is not null)
        {
            _route.DelayMs = Read(e.NewValue, -1);
        }
    }
}
