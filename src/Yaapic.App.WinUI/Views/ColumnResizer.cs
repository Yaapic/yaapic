using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Yaapic.App.WinUI.Views;

/// <summary>Makes a <see cref="SplitterHandle"/> resize the left column of a two-pane grid: drag, arrow keys and double click (reset).</summary>
internal sealed class ColumnResizer
{
    private const double Step = 16;
    private const double MinRightWidth = 320;
    private readonly Grid _host;
    private readonly ColumnDefinition _column;
    private readonly SplitterHandle _handle;
    private readonly double _defaultWidth;
    private bool _dragging;
    private double _lastX;

    /// <summary>Wires the handle.</summary>
    /// <param name="host">The grid that owns the column.</param>
    /// <param name="column">The left column; its <c>MinWidth</c> and <c>MaxWidth</c> bound the width and its current width is the default.</param>
    /// <param name="handle">The splitter handle.</param>
    public ColumnResizer(Grid host, ColumnDefinition column, SplitterHandle handle)
    {
        _host = host;
        _column = column;
        _handle = handle;
        _defaultWidth = column.Width.Value;
        handle.SetVertical(true);
        handle.PointerPressed += OnPressed;
        handle.PointerMoved += OnMoved;
        handle.PointerReleased += OnReleased;
        handle.PointerCanceled += OnReleased;
        handle.PointerCaptureLost += OnReleased;
        handle.KeyDown += OnKeyDown;
        handle.DoubleTapped += (_, _) => SetWidth(_defaultWidth);
        host.SizeChanged += (_, _) => SetWidth(_column.ActualWidth); // a narrower window keeps the results pane usable
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
            SetWidth(_column.ActualWidth + x - _lastX);
            _lastX = x;
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        _handle.ReleasePointerCaptures();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Left: SetWidth(_column.ActualWidth - Step); break;
            case Windows.System.VirtualKey.Right: SetWidth(_column.ActualWidth + Step); break;
            default: return;
        }

        e.Handled = true;
    }

    private void SetWidth(double width)
    {
        var max = Math.Max(_column.MinWidth, Math.Min(_column.MaxWidth, _host.ActualWidth - MinRightWidth));
        _column.Width = new GridLength(Math.Clamp(width, _column.MinWidth, max));
    }
}
