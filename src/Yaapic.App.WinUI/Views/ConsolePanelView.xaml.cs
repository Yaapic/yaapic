using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Yaapic.App.WinUI.Presentation;
using Yaapic.ViewModels.Console;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.WinUI.Views;

/// <summary>The resizable console panel above the status bar, bound to the <see cref="ConsoleViewModel"/> of a shell.</summary>
public sealed partial class ConsolePanelView : UserControl
{
    /// <summary>Identifies the <see cref="Shell"/> dependency property.</summary>
    public static readonly DependencyProperty ShellProperty = DependencyProperty.Register(
        nameof(Shell), typeof(ShellViewModel), typeof(ConsolePanelView),
        new PropertyMetadata(null, (d, e) => ((ConsolePanelView)d).OnShellChanged((ShellViewModel?)e.OldValue)));

    private const double SplitterStep = 16;
    private const double BottomTolerance = 24;

    private ScrollViewer? _scroller;
    private bool _splitterDragging;
    private double _splitterLastY;
    private bool _syncing;

    // Auto-scroll rule: follow the newest line while the view is at the bottom; once the user scrolls up, keep their offset
    // until they scroll back to the bottom. The list is rebuilt on every change, so the offset is restored after each rebuild.
    private bool _follow = true;
    private double _offset;
    private bool _rebuilding;
    private bool _userInput;
    private bool _inputHooked;

    /// <summary>Initializes the view.</summary>
    public ConsolePanelView()
    {
        InitializeComponent();
        Lines.Loaded += OnLinesLoaded;
    }

    /// <summary>Gets or sets the shell whose console, height and visibility the panel shows.</summary>
    public ShellViewModel? Shell
    {
        get => (ShellViewModel?)GetValue(ShellProperty);
        set => SetValue(ShellProperty, value);
    }

    private ConsoleViewModel? ViewModel => Shell?.Console;

    private void OnShellChanged(ShellViewModel? old)
    {
        if (old is not null)
        {
            ((INotifyCollectionChanged)old.Console.Lines).CollectionChanged -= OnLinesChanged;
            old.PropertyChanged -= OnShellPropertyChanged;
        }

        if (Shell is { } shell)
        {
            ((INotifyCollectionChanged)shell.Console.Lines).CollectionChanged += OnLinesChanged;
            shell.PropertyChanged += OnShellPropertyChanged;
            _syncing = true;
            FilterChips.SelectedIndex = (int)shell.Console.Filter;
            _syncing = false;
        }

        Bindings?.Update();
    }

    // The list has no scroll viewer while the panel is collapsed: apply the scroll rule once it opens.
    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsConsoleOpen) && Shell?.IsConsoleOpen == true)
        {
            QueueRestore();
        }
    }

    private void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && ViewModel is { } vm && FilterChips.SelectedIndex >= 0)
        {
            vm.Filter = (ConsoleFilter)FilterChips.SelectedIndex;
        }
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && ViewModel is { } vm)
        {
            vm.Search = "";
            e.Handled = true;
        }
    }

    private void OnLinesLoaded(object sender, RoutedEventArgs e)
    {
        if (!_inputHooked)
        {
            // Wheel, scrollbar drag and keys mark a view change as the user's (handled events included).
            Lines.AddHandler(PointerWheelChangedEvent, new PointerEventHandler((_, _) => _userInput = true), true);
            Lines.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _userInput = true), true);
            Lines.AddHandler(KeyDownEvent, new KeyEventHandler((_, _) => _userInput = true), true);
            Lines.LayoutUpdated += OnLinesLayoutUpdated;
            _inputHooked = true;
        }

        if (_scroller is null && FindScroller(Lines) is { } scroller)
        {
            _scroller = scroller;
            scroller.ViewChanged += OnViewChanged;
        }

        // The panel was collapsed while lines arrived: apply the scroll rule now that the list exists.
        QueueRestore();
    }

    private static ScrollViewer? FindScroller(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            if (FindScroller(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    // The extent of a virtualised list settles after the first layout passes: keep the bottom while following.
    private void OnLinesLayoutUpdated(object? sender, object e)
    {
        // The template of a list in a collapsed panel is only built by the first layout after it opens.
        if (_scroller is null && FindScroller(Lines) is { } found)
        {
            _scroller = found;
            found.ViewChanged += OnViewChanged;
            QueueRestore();
        }

        if (_follow && !_userInput && _scroller is { } scroller && scroller.VerticalOffset < scroller.ScrollableHeight - 1)
        {
            scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true);
        }
    }

    private void OnViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (_rebuilding || e.IsIntermediate || _scroller is not { } scroller)
        {
            return;
        }

        var atBottom = scroller.VerticalOffset >= scroller.ScrollableHeight - BottomTolerance;
        if (_userInput)
        {
            _userInput = false;
            _offset = scroller.VerticalOffset;
            _follow = atBottom;
        }
        else if (_follow && !atBottom)
        {
            // The extent grew after the scroll (rows are realized lazily): keep following the newest line.
            scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true);
        }
    }

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // A rebuild raises one event per line: coalesce them into a single scroll once the burst is over.
        QueueRestore();
    }

    private void QueueRestore()
    {
        if (!_rebuilding)
        {
            _rebuilding = true;
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, RestoreScroll);
        }
    }

    private void RestoreScroll()
    {
        if (_scroller is null && FindScroller(Lines) is { } found)
        {
            _scroller = found;
            found.ViewChanged += OnViewChanged;
        }

        if (_scroller is { } scroller)
        {
            if (_follow && ViewModel?.Lines is { Count: > 0 } lines)
            {
                // Realizes the last row (the list is virtualised), then lands exactly on the bottom.
                Lines.ScrollIntoView(lines[^1]);
                scroller.UpdateLayout();
                scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: true);
            }
            else
            {
                scroller.ChangeView(null, _offset, null, disableAnimation: true);
            }
        }

        // Late view-changed events of this scroll still belong to the rebuild.
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => _rebuilding = false);
    }

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Splitter.CapturePointer(e.Pointer))
        {
            _splitterDragging = true;
            _splitterLastY = e.GetCurrentPoint(null).Position.Y;
            Splitter.Focus(FocusState.Pointer);
            UpdateSplitterLine();
        }
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_splitterDragging)
        {
            // Window coordinates: the panel moves under the pointer while it is resized. Dragging up grows the panel.
            var y = e.GetCurrentPoint(null).Position.Y;
            Resize(_splitterLastY - y);
            _splitterLastY = y;
        }
    }

    private void OnSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _splitterDragging = false;
        Splitter.ReleasePointerCaptures();
        UpdateSplitterLine();
    }

    private void OnSplitterDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => SetHeight(ShellViewModel.DefaultConsoleHeight);

    private void OnSplitterKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Up: Resize(SplitterStep); break;
            case Windows.System.VirtualKey.Down: Resize(-SplitterStep); break;
            case Windows.System.VirtualKey.Home: SetHeight(ShellViewModel.DefaultConsoleHeight); break;
            default: return;
        }

        e.Handled = true;
    }

    private void OnSplitterPointerEntered(object sender, PointerRoutedEventArgs e) => UpdateSplitterLine(hovered: true);

    private void OnSplitterPointerExited(object sender, PointerRoutedEventArgs e) => UpdateSplitterLine();

    private void OnSplitterFocusChanged(object sender, RoutedEventArgs e) => UpdateSplitterLine();

    private void UpdateSplitterLine(bool hovered = false)
    {
        var active = hovered || _splitterDragging || Splitter.FocusState != FocusState.Unfocused;
        SplitterLine.Height = _splitterDragging ? 2 : 1;
        SplitterLine.Fill = BrushMap.Themed(active ? "YaapicAccentFillBrush" : "YaapicBorderDefaultBrush");
    }

    // ConsoleHeight clamps to 120..600 itself.
    private void Resize(double delta)
    {
        if (Shell is { } shell)
        {
            shell.ConsoleHeight += delta;
        }
    }

    private void SetHeight(double height)
    {
        if (Shell is { } shell)
        {
            shell.ConsoleHeight = height;
        }
    }
}
