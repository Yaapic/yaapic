using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.Avalonia.Views;

/// <summary>
/// The resizable console panel. Auto-scroll rule: the list follows the newest line while the view is at the bottom (24 px tolerance);
/// scrolling away stops following, and the offset is restored after each rebuild until the user scrolls back to the bottom.
/// </summary>
public sealed partial class ConsolePanelView : UserControl
{
    private const double BottomTolerance = 24;
    private const double KeyStep = 16;
    private ShellViewModel? _shell;
    private bool _follow = true;
    private bool _rebuilding;
    private double _offset;

    /// <summary>Initializes the view.</summary>
    public ConsolePanelView()
    {
        InitializeComponent();
        Lines.AddHandler(ScrollViewer.ScrollChangedEvent, OnScrollChanged);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_shell is not null)
        {
            _shell.PropertyChanged -= OnShellChanged;
            ((INotifyCollectionChanged)_shell.Console.Lines).CollectionChanged -= OnLinesChanged;
        }

        _shell = DataContext as ShellViewModel;
        if (_shell is not null)
        {
            _shell.PropertyChanged += OnShellChanged;
            ((INotifyCollectionChanged)_shell.Console.Lines).CollectionChanged += OnLinesChanged;
            FilterChips.SelectedIndex = (int)_shell.Console.Filter;
        }
    }

    // The scroll viewer of a hidden panel has no layout: scroll again once the panel opens.
    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsConsoleOpen) && _shell is { IsConsoleOpen: true })
        {
            QueueRestore();
        }
    }

    // The view model rebuilds the whole list on every change: coalesce the burst of events into one scroll restore.
    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e) => QueueRestore();

    private void QueueRestore()
    {
        if (_rebuilding)
        {
            return;
        }

        _rebuilding = true;
        Dispatcher.UIThread.Post(Restore, DispatcherPriority.Loaded);
    }

    private void Restore()
    {
        if (Lines.Scroll is ScrollViewer viewer)
        {
            if (_follow)
            {
                viewer.ScrollToEnd();
            }
            else
            {
                viewer.Offset = viewer.Offset.WithY(_offset);
            }
        }

        // Scroll events caused by the restore itself arrive after layout: keep ignoring them until then.
        Dispatcher.UIThread.Post(() => _rebuilding = false, DispatcherPriority.Background);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_rebuilding || Lines.Scroll is not ScrollViewer viewer)
        {
            return;
        }

        _offset = viewer.Offset.Y;
        _follow = viewer.Offset.Y >= viewer.Extent.Height - viewer.Viewport.Height - BottomTolerance;
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_shell is { } shell && FilterChips.SelectedIndex >= 0)
        {
            shell.Console.Filter = (ConsoleFilter)FilterChips.SelectedIndex;
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _shell is { } shell)
        {
            shell.Console.Search = "";
            e.Handled = true;
        }
    }

    private void OnSplitterDrag(object? sender, VectorEventArgs e)
    {
        if (_shell is { } shell)
        {
            shell.ConsoleHeight = Math.Min(shell.ConsoleHeight - e.Vector.Y, MaxHeight);
        }
    }

    private void OnSplitterKeyDown(object? sender, KeyEventArgs e)
    {
        if (_shell is not { } shell || e.Key is not (Key.Up or Key.Down))
        {
            return;
        }

        shell.ConsoleHeight = Math.Min(shell.ConsoleHeight + (e.Key == Key.Up ? KeyStep : -KeyStep), MaxHeight);
        e.Handled = true;
    }

    private void OnSplitterDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_shell is { } shell)
        {
            shell.ConsoleHeight = ShellViewModel.DefaultConsoleHeight;
        }
    }
}
