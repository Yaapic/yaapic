using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Yaapic.Core.Preferences;

namespace Yaapic.App.WinUI.Views;

/// <summary>
/// Makes a <see cref="SplitterHandle"/> resize the left pane of a layout: drag, arrow keys and double click (reset). The width is
/// remembered through <see cref="PaneLayoutSettings"/>, saved when a drag ends and applied from the stored value.
/// </summary>
internal sealed class ColumnResizer
{
    private const double Step = 16;
    private readonly Grid _host;
    private readonly SplitterHandle _handle;
    private readonly PaneId _pane;
    private readonly PaneLayoutSettings _settings;
    private readonly double _reserved;
    private readonly double _capFloor;
    private readonly Action<double> _apply;
    private double _desired;
    private double _current;
    private bool _dragging;
    private double _lastX;

    /// <summary>Wires a handle that resizes a grid column.</summary>
    /// <param name="host">The grid that owns the column.</param>
    /// <param name="column">The left column.</param>
    /// <param name="handle">The splitter handle.</param>
    /// <param name="pane">The pane, which gives the limits and the remembered width.</param>
    /// <param name="settings">The remembered widths.</param>
    public ColumnResizer(Grid host, ColumnDefinition column, SplitterHandle handle, PaneId pane, PaneLayoutSettings settings)
        : this(host, handle, pane, settings, 320, PaneLimits.Min(pane), w => column.Width = new GridLength(w))
    {
        column.MinWidth = PaneLimits.Min(pane);
        column.MaxWidth = PaneLimits.Max(pane);
        Apply(_desired);
    }

    /// <summary>Wires a handle that resizes the width of an element (a pane whose column is <c>Auto</c> and collapses with it).</summary>
    /// <param name="host">The grid that holds the whole layout.</param>
    /// <param name="target">The element whose width is resized.</param>
    /// <param name="handle">The splitter handle.</param>
    /// <param name="pane">The pane, which gives the limits and the remembered width.</param>
    /// <param name="settings">The remembered widths.</param>
    /// <param name="reserved">The width the rest of the layout needs next to the pane; a narrow window shrinks the pane towards its default width, no further.</param>
    public ColumnResizer(Grid host, FrameworkElement target, SplitterHandle handle, PaneId pane, PaneLayoutSettings settings, double reserved)
        : this(host, handle, pane, settings, reserved, PaneLimits.Default(pane), w => target.Width = w)
    {
        target.MinWidth = PaneLimits.Min(pane);
        target.MaxWidth = PaneLimits.Max(pane);
        Apply(_desired);
    }

    private ColumnResizer(Grid host, SplitterHandle handle, PaneId pane, PaneLayoutSettings settings, double reserved, double capFloor, Action<double> apply)
    {
        _host = host;
        _handle = handle;
        _pane = pane;
        _settings = settings;
        _reserved = reserved;
        _capFloor = capFloor;
        _apply = apply;
        _desired = settings.Get(pane);
        handle.SetVertical(true);
        handle.PointerPressed += OnPressed;
        handle.PointerMoved += OnMoved;
        handle.PointerReleased += OnReleased;
        handle.PointerCanceled += OnReleased;
        handle.PointerCaptureLost += OnReleased;
        handle.KeyDown += OnKeyDown;
        handle.DoubleTapped += (_, _) => Commit(PaneLimits.Default(pane));
        host.SizeChanged += (_, _) => Apply(_desired); // a narrower window keeps the other pane usable; a wider one gives the width back
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_handle.CapturePointer(e.Pointer))
        {
            _dragging = true;
            _lastX = e.GetCurrentPoint(null).Position.X;
            _handle.Focus(FocusState.Pointer);
        }
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            var x = e.GetCurrentPoint(null).Position.X;
            Apply(_current + x - _lastX);
            _lastX = x;
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging)
        {
            _dragging = false;
            _ = _settings.SetAsync(_pane, _desired); // once per drag, not on every pixel
        }

        _handle.ReleasePointerCaptures();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left: Commit(_current - Step); break;
            case Windows.System.VirtualKey.Right: Commit(_current + Step); break;
            default: return;
        }

        e.Handled = true;
    }

    private void Commit(double width)
    {
        Apply(width);
        _ = _settings.SetAsync(_pane, _desired);
    }

    // Sets the wanted width, clamped to the limits and to what the window leaves; the wanted width itself only changes by the user.
    private void Apply(double wanted)
    {
        var limited = PaneLimits.Clamp(_pane, wanted);
        _desired = limited;
        var max = _host.ActualWidth > 0 ? Math.Min(PaneLimits.Max(_pane), _host.ActualWidth - _reserved) : PaneLimits.Max(_pane);
        _current = Math.Max(PaneLimits.Min(_pane), Math.Min(limited, Math.Max(_capFloor, max)));
        _apply(_current);
    }
}
