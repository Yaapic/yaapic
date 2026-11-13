using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Yaapic.Core.Variables;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>
/// A single-line text box that colours each <c>{{variable}}</c> by how it resolves and opens a details popup on hover.
/// A transparent <see cref="TextBox"/> keeps every editing behaviour while a <see cref="TextBlock"/> behind it draws the colours.
/// </summary>
public sealed partial class VariableTextBox : UserControl
{
    private static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>Identifies the <see cref="Text"/> dependency property.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(VariableTextBox),
        new PropertyMetadata("", (d, e) => ((VariableTextBox)d).OnTextPropertyChanged((string?)e.NewValue ?? "")));

    /// <summary>Identifies the <see cref="PlaceholderText"/> dependency property.</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText), typeof(string), typeof(VariableTextBox),
        new PropertyMetadata("", (d, e) => ((VariableTextBox)d).Input.PlaceholderText = (string?)e.NewValue ?? ""));

    /// <summary>Identifies the <see cref="Context"/> dependency property.</summary>
    public static readonly DependencyProperty ContextProperty = DependencyProperty.Register(
        nameof(Context), typeof(VariableContext), typeof(VariableTextBox),
        new PropertyMetadata(null, (d, e) => ((VariableTextBox)d).OnContextChanged((VariableContext?)e.OldValue, (VariableContext?)e.NewValue)));

    /// <summary>Identifies the <see cref="InputName"/> dependency property.</summary>
    public static readonly DependencyProperty InputNameProperty = DependencyProperty.Register(
        nameof(InputName), typeof(string), typeof(VariableTextBox),
        new PropertyMetadata("", (d, e) => Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(((VariableTextBox)d).Input, (string?)e.NewValue ?? "")));

    /// <summary>Identifies the <see cref="InputPadding"/> dependency property.</summary>
    public static readonly DependencyProperty InputPaddingProperty = DependencyProperty.Register(
        nameof(InputPadding), typeof(Thickness), typeof(VariableTextBox),
        new PropertyMetadata(new Thickness(10, 7, 10, 5), (d, e) => ((VariableTextBox)d).ApplyPadding((Thickness)e.NewValue)));

    /// <summary>Identifies the <see cref="CodeFontSize"/> dependency property.</summary>
    public static readonly DependencyProperty CodeFontSizeProperty = DependencyProperty.Register(
        nameof(CodeFontSize), typeof(double), typeof(VariableTextBox),
        new PropertyMetadata(13.0, (d, e) => ((VariableTextBox)d).ApplyFontSize((double)e.NewValue)));

    /// <summary>Identifies the <see cref="IsReadOnly"/> dependency property.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(VariableTextBox),
        new PropertyMetadata(false, (d, e) => ((VariableTextBox)d).Input.IsReadOnly = (bool)e.NewValue));

    private static readonly SolidColorBrush TransparentBrush = new(Microsoft.UI.Colors.Transparent);

    private readonly DispatcherTimer _openTimer = new() { Interval = OpenDelay };
    private readonly DispatcherTimer _closeTimer = new() { Interval = CloseDelay };
    private IReadOnlyList<VariableToken> _tokens = [];
    private string _renderedText = "";
    private bool _syncing;
    private bool _attached;
    private ScrollViewer? _scroller;
    private FrameworkElement? _themeRoot;
    private VariableToken? _pending;
    private VariableToken? _shown;
    private VariablePopupView? _popupView;
    private VariablePopupViewModel? _popupModel;
    private bool _overPopup;

    /// <summary>Initializes the box.</summary>
    public VariableTextBox()
    {
        InitializeComponent();
        ApplyPadding(InputPadding);
        Host.AddHandler(PointerMovedEvent, new PointerEventHandler(OnHostPointerMoved), true);
        Host.PointerExited += (_, _) => OnPointerLeftToken();
        _openTimer.Tick += OnOpenTimerTick;
        _closeTimer.Tick += OnCloseTimerTick;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Gets or sets the text; changes flow both ways as the user types.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets the hint shown while the box is empty.</summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Gets or sets the context that tokenises the text; <see langword="null"/> makes this a plain text box.</summary>
    public VariableContext? Context
    {
        get => (VariableContext?)GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    /// <summary>Gets or sets the UI Automation name of the editable box.</summary>
    public string InputName
    {
        get => (string)GetValue(InputNameProperty);
        set => SetValue(InputNameProperty, value);
    }

    /// <summary>Gets or sets the padding of the text inside the box.</summary>
    public Thickness InputPadding
    {
        get => (Thickness)GetValue(InputPaddingProperty);
        set => SetValue(InputPaddingProperty, value);
    }

    /// <summary>Gets or sets the font size of the text and of its coloured overlay.</summary>
    public double CodeFontSize
    {
        get => (double)GetValue(CodeFontSizeProperty);
        set => SetValue(CodeFontSizeProperty, value);
    }

    /// <summary>Gets or sets whether the text can be selected but not edited.</summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Sets <paramref name="context"/> on every <see cref="VariableTextBox"/> inside a realised table row.</summary>
    internal static void ApplyContext(DependencyObject root, VariableContext? context)
    {
        if (root is VariableTextBox box)
        {
            box.Context = context;
            return;
        }

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            ApplyContext(VisualTreeHelper.GetChild(root, i), context);
        }
    }

    private void ApplyFontSize(double size)
    {
        Input.FontSize = size;
        Overlay.FontSize = size;
        SelectionOverlay.FontSize = size;
        Render(force: true);
    }

    private void ApplyPadding(Thickness padding)
    {
        Input.Padding = padding;
        OverlayClip.Padding = padding;
        SelectionClip.Padding = padding;
    }

    private void OnTextPropertyChanged(string value)
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            if (Input.Text != value)
            {
                Input.Text = value;
            }
        }
        finally
        {
            _syncing = false;
        }

        Render();
        RenderSelection();
        CloseIfTokenGone();
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_syncing)
        {
            _syncing = true;
            try
            {
                Text = Input.Text;
            }
            finally
            {
                _syncing = false;
            }
        }

        Render();
        CloseIfTokenGone();
        UpdateCompletion();
    }

    private void OnContextChanged(VariableContext? oldValue, VariableContext? newValue)
    {
        if (_attached)
        {
            Unsubscribe(oldValue);
            Subscribe(newValue);
        }

        ClosePopup();
        CloseCompletion();
        Render();
    }

    private void Subscribe(VariableContext? context)
    {
        if (context is not null)
        {
            context.Changed += OnContextRefresh;
            context.Activated += OnContextRefresh;
            context.OpenRequested += OnContextOpenRequested;
        }
    }

    private void Unsubscribe(VariableContext? context)
    {
        if (context is not null)
        {
            context.Changed -= OnContextRefresh;
            context.Activated -= OnContextRefresh;
            context.OpenRequested -= OnContextOpenRequested;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_attached)
        {
            return;
        }

        _attached = true;
        // The theme event is not raised on this control when the window root changes theme, so listen on the root.
        _themeRoot = XamlRoot?.Content as FrameworkElement;
        if (_themeRoot is not null)
        {
            _themeRoot.ActualThemeChanged += OnRootThemeChanged;
        }

        Subscribe(Context);
        Input.ApplyTemplate();
        _scroller = FindScroller(Input);
        if (_scroller is not null)
        {
            _scroller.ViewChanged += OnScrollChanged;
        }

        Render(force: true);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _attached = false;
        if (_themeRoot is not null)
        {
            _themeRoot.ActualThemeChanged -= OnRootThemeChanged;
            _themeRoot = null;
        }

        ClosePopup();
        CloseCompletion();
        Unsubscribe(Context);
        if (_scroller is not null)
        {
            _scroller.ViewChanged -= OnScrollChanged;
            _scroller = null;
        }
    }

    private void OnRootThemeChanged(FrameworkElement sender, object args) =>
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => Render(force: true));

    private void OnContextRefresh(object? sender, EventArgs e)
    {
        Render(force: true);
        CloseIfTokenGone();
    }

    private void OnContextOpenRequested(object? sender, VariableScopeKind e) => ClosePopup();

    private static ScrollViewer? FindScroller(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer { Name: "ContentElement" } viewer)
            {
                return viewer;
            }

            if (FindScroller(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private void OnScrollChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        OverlayShift.X = SelectionShift.X = -(_scroller?.HorizontalOffset ?? 0);
    }

    private void OnOverlayCanvasSizeChanged(object sender, SizeChangedEventArgs e) => CenterOverlay();

    private void OnOverlayTextSizeChanged(object sender, SizeChangedEventArgs e) => CenterOverlay();

    // The box draws its text about 2 px above the centre of its content area (font ascent), so the overlays sit there too.
    private void CenterOverlay()
    {
        var top = (OverlayCanvas.ActualHeight - Overlay.ActualHeight) / 2 - 2;
        Canvas.SetTop(Overlay, top);
        Canvas.SetTop(SelectionOverlay, top);
    }

    private void OnInputSelectionChanged(object sender, RoutedEventArgs e)
    {
        RenderSelection();
        if (IsCompletionOpen)
        {
            UpdateCompletion();
        }
    }

    private void RenderSelection()
    {
        var text = Input.Text;
        var start = Math.Min(Input.SelectionStart, text.Length);
        var length = Math.Min(Input.SelectionLength, text.Length - start);
        SelectionOverlay.Inlines.Clear();
        if (length == 0 || Input.FocusState == FocusState.Unfocused)
        {
            SelectionOverlay.Text = "";
            return;
        }

        SelectionOverlay.Inlines.Add(new Run { Text = text[..start], Foreground = TransparentBrush });
        SelectionOverlay.Inlines.Add(new Run { Text = text.Substring(start, length), Foreground = Resolve("YaapicAccentOnBrush") });
        SelectionOverlay.Inlines.Add(new Run { Text = text[(start + length)..], Foreground = TransparentBrush });
    }

    private void OnHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var padding = InputPadding;
        var rect = new Rect(padding.Left, 0, Math.Max(0, e.NewSize.Width - padding.Left - padding.Right), e.NewSize.Height);
        OverlayClip.Clip = new RectangleGeometry { Rect = rect };
        SelectionClip.Clip = new RectangleGeometry { Rect = rect };
    }

    // Rebuilds the coloured runs; the text of the overlay always equals the text of the box, character for character.
    private void Render(bool force = false)
    {
        var text = Input.Text;
        var tokens = Context?.Tokenize(text) ?? [];
        if (!force && text == _renderedText && tokens.SequenceEqual(_tokens))
        {
            return;
        }

        _renderedText = text;
        _tokens = tokens;
        CenterOverlay();
        Overlay.TextHighlighters.Clear();
        Overlay.Inlines.Clear();
        if (tokens.Count == 0)
        {
            Overlay.Text = text;
            return;
        }

        var highlighters = new Dictionary<VariableTokenState, TextHighlighter>();
        var position = 0;
        foreach (var token in tokens)
        {
            if (token.Start > position)
            {
                Overlay.Inlines.Add(new Run { Text = text[position..token.Start] });
            }

            var (foreground, background) = Colors(token.State);
            Overlay.Inlines.Add(new Run { Text = text[token.Start..token.End], Foreground = foreground });
            if (!highlighters.TryGetValue(token.State, out var highlighter))
            {
                highlighter = new TextHighlighter { Foreground = foreground, Background = background };
                highlighters[token.State] = highlighter;
                Overlay.TextHighlighters.Add(highlighter);
            }

            highlighter.Ranges.Add(new TextRange { StartIndex = token.Start, Length = token.Length });
            position = token.End;
        }

        if (position < text.Length)
        {
            Overlay.Inlines.Add(new Run { Text = text[position..] });
        }
    }

    private (Brush Foreground, Brush Background) Colors(VariableTokenState state) => state switch
    {
        VariableTokenState.Resolved => (Resolve("YaapicSuccessBrush"), Resolve("YaapicSuccessBackgroundBrush")),
        VariableTokenState.Dynamic => (Resolve("YaapicInfoBrush"), Resolve("YaapicInfoBackgroundBrush")),
        _ => (Resolve("YaapicWarningBrush"), Resolve("YaapicWarningBackgroundBrush")),
    };

    // A code lookup in Application.Resources only sees the application theme, so the theme dictionary of the element theme is searched first.
    private Brush Resolve(string key)
    {
        var theme = ActualTheme == ElementTheme.Light ? "Light" : "Dark";
        return FindThemed(Application.Current.Resources, theme, key) ?? (Brush)Application.Current.Resources[key];
    }

    private static Brush? FindThemed(ResourceDictionary dictionary, string theme, string key)
    {
        if (dictionary.ThemeDictionaries.TryGetValue(theme, out var themed)
            && themed is ResourceDictionary colors
            && colors.TryGetValue(key, out var value)
            && value is Brush brush)
        {
            return brush;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (FindThemed(merged, theme, key) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private VariableToken? TokenAt(Point point)
    {
        if (_tokens.Count == 0 || point.Y < 0 || point.Y > Input.ActualHeight || point.X < 0 || point.X > Input.ActualWidth)
        {
            return null;
        }

        foreach (var token in _tokens)
        {
            if (token.End > Input.Text.Length)
            {
                return null;
            }

            var left = Input.GetRectFromCharacterIndex(token.Start, false);
            var right = Input.GetRectFromCharacterIndex(token.End - 1, true);
            if (point.X >= left.X && point.X <= right.X + right.Width)
            {
                return token;
            }
        }

        return null;
    }

    private void OnHostPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (Context is null)
        {
            return;
        }

        var point = e.GetCurrentPoint(Input);
        if (point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (TokenAt(point.Position) is not { } token)
        {
            OnPointerLeftToken();
            return;
        }

        _closeTimer.Stop();
        if (_shown is not null && SameToken(_shown, token))
        {
            _openTimer.Stop();
            _pending = null;
            return;
        }

        if (_pending is null || !SameToken(_pending, token))
        {
            _pending = token;
            _openTimer.Stop();
            _openTimer.Start();
        }
    }

    private static bool SameToken(VariableToken a, VariableToken b) => a.Start == b.Start && a.Length == b.Length;

    private void OnPointerLeftToken()
    {
        _openTimer.Stop();
        _pending = null;
        if (_shown is not null && !_overPopup)
        {
            _closeTimer.Stop();
            _closeTimer.Start();
        }
    }

    private void OnOpenTimerTick(object? sender, object e)
    {
        _openTimer.Stop();
        if (_pending is { } token)
        {
            _pending = null;
            ShowPopup(token, focusPopup: false);
        }
    }

    private void OnCloseTimerTick(object? sender, object e)
    {
        _closeTimer.Stop();
        if (_overPopup)
        {
            return;
        }

        // A dropdown of the card opens its own popup, so the pointer leaves the card while the user is still working in it.
        var dropdownOpen = XamlRoot is not null
            && VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot).Any(p => !ReferenceEquals(p, VariablePopup));
        if (dropdownOpen)
        {
            _closeTimer.Start();
        }
        else if (_popupView?.ContainsFocus() != true)
        {
            ClosePopup();
        }
    }

    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (HandleCompletionKey(e))
        {
            return;
        }

        if (e.Key == VirtualKey.Space && IsControlDown())
        {
            OpenCompletionManually();
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape && _shown is not null)
        {
            ClosePopup();
            e.Handled = true;
        }
        else if (e.Key == (VirtualKey)190 && IsControlDown() && Context is not null)
        {
            var caret = Input.SelectionStart;
            if (_tokens.FirstOrDefault(t => caret >= t.Start && caret <= t.End) is { } token)
            {
                ShowPopup(token, focusPopup: true);
                e.Handled = true;
            }
        }
    }

    private static bool IsControlDown() =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void ShowPopup(VariableToken token, bool focusPopup)
    {
        if (Context is not { } context || XamlRoot is null)
        {
            return;
        }

        _closeTimer.Stop();
        DetachPopup();
        _shown = token;
        _popupModel = context.CreatePopup(token.Name);
        _popupModel.Submitted += OnPopupSubmitted;
        _popupView = new VariablePopupView(_popupModel, token.State);
        _popupView.PointerEntered += (_, _) =>
        {
            _overPopup = true;
            _closeTimer.Stop();
        };
        _popupView.PointerExited += (_, _) =>
        {
            _overPopup = false;
            _closeTimer.Stop();
            _closeTimer.Start();
        };
        _popupView.CloseRequested += OnPopupCloseRequested;
        _popupView.LostFocus += (_, _) => CloseIfFocusLeft();
        VariablePopup.Child = _popupView;
        Place(token);
        VariablePopup.XamlRoot = XamlRoot;
        VariablePopup.IsOpen = true;
        if (focusPopup)
        {
            _popupView.FocusFirst();
        }
    }

    private void Place(VariableToken token)
    {
        var left = Input.GetRectFromCharacterIndex(token.Start, false);
        _popupView!.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = _popupView.DesiredSize.Width;
        var origin = Host.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var x = Math.Min(Math.Max(0, left.X), XamlRoot.Size.Width - 8 - width - origin.X);
        VariablePopup.HorizontalOffset = Math.Max(8 - origin.X, x);
        VariablePopup.VerticalOffset = Host.ActualHeight + 4;
    }

    private void OnPopupSubmitted(object? sender, EventArgs e)
    {
        // The token is recoloured through Context.Changed; the popup now describes the saved variable.
        if (_shown is { } token && Context is not null)
        {
            var refocus = _popupView?.ContainsFocus() == true;
            Render(force: true);
            ShowPopup(_tokens.FirstOrDefault(t => t.Name == token.Name) ?? token, refocus);
        }
    }

    private void OnPopupCloseRequested(object? sender, EventArgs e)
    {
        ClosePopup();
        Input.Focus(FocusState.Programmatic);
    }

    private void OnInputGotFocus(object sender, RoutedEventArgs e) => RenderSelection();

    private void OnInputLostFocus(object sender, RoutedEventArgs e)
    {
        CloseCompletion();
        RenderSelection();
        CloseIfFocusLeft();
    }

    private void CloseIfFocusLeft() =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (_shown is null || _overPopup)
            {
                return;
            }

            var focused = XamlRoot is null ? null : FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            if (_popupView?.Contains(focused) != true && !ReferenceEquals(focused, Input))
            {
                ClosePopup();
            }
        });

    private void CloseIfTokenGone()
    {
        if (_shown is { } token && !_tokens.Any(t => t.Name == token.Name))
        {
            ClosePopup();
        }
    }

    private void DetachPopup()
    {
        if (_popupModel is not null)
        {
            _popupModel.Submitted -= OnPopupSubmitted;
        }

        if (_popupView is not null)
        {
            _popupView.CloseRequested -= OnPopupCloseRequested;
        }

        _popupModel = null;
        _popupView = null;
    }

    private void ClosePopup()
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        _pending = null;
        _shown = null;
        _overPopup = false;
        VariablePopup.IsOpen = false;
        VariablePopup.Child = null;
        DetachPopup();
    }
}
