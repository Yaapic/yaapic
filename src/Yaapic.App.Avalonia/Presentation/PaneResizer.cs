using Avalonia.Controls;
using Avalonia.Input;
using Yaapic.Core.Preferences;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Resizes a pane, remembers its width and resets it on double click; the width is saved when a drag ends, not on every pixel.</summary>
internal static class PaneResizer
{
    private const double Step = 16;

    /// <summary>Wires a <see cref="GridSplitter"/> that resizes the left column of a two-pane grid.</summary>
    /// <param name="column">The left column.</param>
    /// <param name="splitter">The splitter between the columns.</param>
    /// <param name="pane">The pane, which gives the limits and the remembered width.</param>
    /// <param name="settings">The remembered widths.</param>
    public static void Attach(ColumnDefinition column, GridSplitter splitter, PaneId pane, PaneLayoutSettings settings)
    {
        column.MinWidth = PaneLimits.Min(pane);
        column.MaxWidth = PaneLimits.Max(pane);
        column.Width = new GridLength(settings.Get(pane));
        void Save() => _ = settings.SetAsync(pane, column.Width.Value);
        splitter.DragCompleted += (_, _) => Save();
        splitter.KeyUp += (_, e) =>
        {
            if (e.Key is Key.Left or Key.Right)
            {
                Save(); // the splitter moves by the arrow keys itself
            }
        };
        splitter.DoubleTapped += (_, _) =>
        {
            column.Width = new GridLength(PaneLimits.Default(pane));
            Save();
        };
    }

    /// <summary>Wires a handle that resizes the width of a pane whose column is <c>Auto</c>, so that the column collapses with the pane.</summary>
    /// <param name="host">The layout that holds the pane; its width bounds the pane so that <paramref name="reserved"/> is left to the rest.</param>
    /// <param name="target">The pane.</param>
    /// <param name="handle">The handle, a focusable control placed on the right edge of the pane.</param>
    /// <param name="pane">The pane, which gives the limits and the remembered width.</param>
    /// <param name="settings">The remembered widths.</param>
    /// <param name="reserved">The width the rest of the layout needs next to the pane; a narrow window shrinks the pane towards its default width, no further.</param>
    public static void Attach(Control host, Control target, Control handle, PaneId pane, PaneLayoutSettings settings, double reserved)
    {
        var desired = settings.Get(pane);
        var current = desired;
        var dragging = false;
        var lastX = 0.0;
        target.MinWidth = PaneLimits.Min(pane);
        target.MaxWidth = PaneLimits.Max(pane);
        handle.Focusable = true;

        // The wanted width is clamped to the limits and to what the window leaves; only the user changes the wanted width.
        void Apply(double wanted)
        {
            desired = PaneLimits.Clamp(pane, wanted);
            var max = host.Bounds.Width > 0 ? Math.Min(PaneLimits.Max(pane), host.Bounds.Width - reserved) : PaneLimits.Max(pane);
            current = Math.Max(PaneLimits.Min(pane), Math.Min(desired, Math.Max(PaneLimits.Default(pane), max)));
            target.Width = current;
        }

        void Commit(double wanted)
        {
            Apply(wanted);
            _ = settings.SetAsync(pane, desired);
        }

        Apply(desired);
        host.SizeChanged += (_, _) => Apply(desired); // a narrower window keeps the editor usable; a wider one gives the width back
        handle.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            {
                dragging = true;
                lastX = e.GetPosition(host).X;
                e.Pointer.Capture(handle);
                handle.Focus();
                e.Handled = true;
            }
        };
        handle.PointerMoved += (_, e) =>
        {
            if (dragging)
            {
                var x = e.GetPosition(host).X;
                Apply(current + x - lastX);
                lastX = x;
            }
        };
        handle.PointerReleased += (_, e) =>
        {
            if (dragging)
            {
                dragging = false;
                e.Pointer.Capture(null);
                _ = settings.SetAsync(pane, desired); // once per drag, not on every pixel
            }
        };
        handle.PointerCaptureLost += (_, _) => dragging = false;
        handle.DoubleTapped += (_, _) => Commit(PaneLimits.Default(pane));
        handle.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Left or Key.Right)
            {
                Commit(current + (e.Key == Key.Left ? -Step : Step));
                e.Handled = true;
            }
        };
    }
}
