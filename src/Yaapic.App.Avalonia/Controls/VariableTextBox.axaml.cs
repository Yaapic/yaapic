using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Yaapic.App.Avalonia.Presentation;
using Yaapic.App.Avalonia.Views;
using Yaapic.Core.Variables;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Controls;

/// <summary>
/// A single-line text box that colours each <c>{{variable}}</c> by how it resolves and opens a details popup on hover.
/// A transparent-text <see cref="TextBox"/> keeps every editing behaviour while a <see cref="TextBlock"/> behind it draws the colours.
/// </summary>
public sealed partial class VariableTextBox : UserControl
{
    private static readonly TimeSpan OpenDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan CloseDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<VariableTextBox, string?>(nameof(Text), "", defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Identifies the <see cref="Watermark"/> property.</summary>
    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<VariableTextBox, string?>(nameof(Watermark));

    /// <summary>Identifies the <see cref="Context"/> property.</summary>
    public static readonly StyledProperty<VariableContext?> ContextProperty =
        AvaloniaProperty.Register<VariableTextBox, VariableContext?>(nameof(Context));

    /// <summary>Identifies the <see cref="InputName"/> property.</summary>
    public static readonly StyledProperty<string?> InputNameProperty =
        AvaloniaProperty.Register<VariableTextBox, string?>(nameof(InputName));

    private readonly DispatcherTimer _openTimer = new() { Interval = OpenDelay };
    private readonly DispatcherTimer _closeTimer = new() { Interval = CloseDelay };
    private readonly Popup _popup;
    private readonly TranslateTransform _shift = new();
    private IReadOnlyList<VariableToken> _tokens = [];
    private string _renderedText = "";
    private bool _syncing;
    private bool _attached;
    private TextPresenter? _presenter;
    private ScrollViewer? _scroller;
    private VariableToken? _pending;
    private VariableToken? _shown;
    private VariablePopupView? _popupView;
    private VariablePopupViewModel? _popupModel;
    private bool _overPopup;

    /// <summary>Creates the box.</summary>
    public VariableTextBox()
    {
        InitializeComponent();
        _popup = new Popup
        {
            PlacementTarget = Host,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = false,
        };
        Host.Children.Add(_popup);
        Overlay.RenderTransform = _shift;
        // The overlay must measure text exactly like the box, so it follows the box font size.
        Overlay.Bind(TextBlock.FontSizeProperty, Input.GetObservable(TemplatedControl.FontSizeProperty));
        Input.TemplateApplied += OnInputTemplateApplied;
        Host.AddHandler(PointerMovedEvent, OnHostPointerMoved, RoutingStrategies.Bubble | RoutingStrategies.Tunnel, handledEventsToo: true);
        Host.PointerExited += (_, _) => OnPointerLeftToken();
        _openTimer.Tick += OnOpenTimerTick;
        _closeTimer.Tick += OnCloseTimerTick;
        ActualThemeVariantChanged += (_, _) => Render(force: true);
    }

    /// <summary>Gets or sets the text; changes flow both ways as the user types.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets or sets the hint shown while the box is empty.</summary>
    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    /// <summary>Gets or sets the context that tokenises the text; <see langword="null"/> makes this a plain text box.</summary>
    public VariableContext? Context
    {
        get => GetValue(ContextProperty);
        set => SetValue(ContextProperty, value);
    }

    /// <summary>Gets or sets the UI Automation name of the editable box.</summary>
    public string? InputName
    {
        get => GetValue(InputNameProperty);
        set => SetValue(InputNameProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            OnTextPropertyChanged(change.GetNewValue<string?>() ?? "");
        }
        else if (change.Property == WatermarkProperty)
        {
            Input.PlaceholderText = change.GetNewValue<string?>();
        }
        else if (change.Property == InputNameProperty)
        {
            global::Avalonia.Automation.AutomationProperties.SetName(Input, change.GetNewValue<string?>() ?? "");
        }
        else if (change.Property == ContextProperty)
        {
            OnContextChanged(change.GetOldValue<VariableContext?>(), change.GetNewValue<VariableContext?>());
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_attached)
        {
            return;
        }

        _attached = true;
        Subscribe(Context);
        Render(force: true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        ClosePopup();
        Unsubscribe(Context);
    }

    private void OnInputTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        if (_scroller is not null)
        {
            _scroller.ScrollChanged -= OnScrollChanged;
        }

        _presenter = e.NameScope.Find<TextPresenter>("PART_TextPresenter");
        _scroller = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (_scroller is not null)
        {
            _scroller.ScrollChanged += OnScrollChanged;
        }
    }

    // The box scrolls its text horizontally when the URL is wider than the bar; the overlay follows it.
    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => _shift.X = -(_scroller?.Offset.X ?? 0);

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
        CloseIfTokenGone();
    }

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
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
    }

    private void OnContextChanged(VariableContext? oldValue, VariableContext? newValue)
    {
        if (_attached)
        {
            Unsubscribe(oldValue);
            Subscribe(newValue);
        }

        ClosePopup();
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

    private void OnContextRefresh(object? sender, EventArgs e)
    {
        // Scope writes can come from another thread of control flow; the overlay is touched on the UI thread only.
        Dispatcher.UIThread.Post(() =>
        {
            Render(force: true);
            CloseIfTokenGone();
        });
    }

    private void OnContextOpenRequested(object? sender, VariableScopeKind e) => ClosePopup();

    // Rebuilds the coloured runs; the text of the overlay always equals the text of the box, character for character.
    private void Render(bool force = false)
    {
        var text = Input.Text ?? "";
        var tokens = Context?.Tokenize(text) ?? [];
        if (!force && text == _renderedText && tokens.SequenceEqual(_tokens))
        {
            return;
        }

        _renderedText = text;
        _tokens = tokens;
        if (tokens.Count == 0)
        {
            Overlay.Inlines?.Clear();
            Overlay.Text = text;
            return;
        }

        var runs = new InlineCollection();
        var position = 0;
        foreach (var token in tokens)
        {
            if (token.Start > position)
            {
                runs.Add(new Run(text[position..token.Start]));
            }

            var (foreground, background) = BrushMap.Token(this, token.State);
            runs.Add(new Run(text[token.Start..token.End]) { Foreground = foreground, Background = background });
            position = token.End;
        }

        if (position < text.Length)
        {
            runs.Add(new Run(text[position..]));
        }

        Overlay.Text = null;
        Overlay.Inlines = runs;
    }

    // Maps a pointer position to the token under it with the text layout of the box, so scrolling is already accounted for.
    private VariableToken? TokenAt(Point pointInPresenter)
    {
        if (_tokens.Count == 0 || _presenter?.TextLayout is not { } layout)
        {
            return null;
        }

        var hit = layout.HitTestPoint(pointInPresenter);
        if (!hit.IsInside)
        {
            return null;
        }

        var index = hit.TextPosition;
        return _tokens.FirstOrDefault(t => index >= t.Start && index < t.End);
    }

    private void OnHostPointerMoved(object? sender, PointerEventArgs e)
    {
        if (Context is null || _presenter is null)
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (TokenAt(e.GetPosition(_presenter)) is not { } token)
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

    private void OnOpenTimerTick(object? sender, EventArgs e)
    {
        _openTimer.Stop();
        if (_pending is { } token)
        {
            _pending = null;
            ShowPopup(token, focusPopup: false);
        }
    }

    private void OnCloseTimerTick(object? sender, EventArgs e)
    {
        _closeTimer.Stop();
        if (_overPopup)
        {
            return;
        }

        // A dropdown of the card opens its own popup, so the pointer leaves the card while the user is still working in it.
        if (_popupView?.HasOpenDropdown == true)
        {
            _closeTimer.Start();
        }
        else if (_popupView?.IsKeyboardFocusWithin != true)
        {
            ClosePopup();
        }
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.Escape && _shown is not null)
        {
            ClosePopup();
            e.Handled = true;
        }
        else if (e.Key == Key.OemPeriod && e.KeyModifiers == modifier && Context is not null)
        {
            var caret = Input.CaretIndex;
            if (_tokens.FirstOrDefault(t => caret >= t.Start && caret <= t.End) is { } token)
            {
                ShowPopup(token, focusPopup: true);
                e.Handled = true;
            }
        }
    }

    private void ShowPopup(VariableToken token, bool focusPopup)
    {
        if (Context is not { } context)
        {
            return;
        }

        _closeTimer.Stop();
        DetachPopup();
        _shown = token;
        _popupModel = context.CreatePopup(token.Name);
        _popupModel.Submitted += OnPopupSubmitted;
        // The folder settings window of the Avalonia app is a later card, so a collection variable has nothing to open.
        _popupView = new VariablePopupView();
        _popupView.Load(_popupModel, token.State, token.Hint.Scope != VariableScopeKind.Collection);
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
        _popup.Child = _popupView;
        Place(token);
        _popup.IsOpen = true;
        if (focusPopup)
        {
            _popupView.FocusFirst();
        }
    }

    private void Place(VariableToken token)
    {
        var x = 0.0;
        if (_presenter?.TextLayout is { } layout)
        {
            var rect = layout.HitTestTextPosition(token.Start);
            x = _presenter.TranslatePoint(rect.TopLeft, Host)?.X ?? 0;
        }

        _popup.HorizontalOffset = Math.Max(0, x);
        _popup.VerticalOffset = 4;
    }

    private void OnPopupSubmitted(object? sender, EventArgs e)
    {
        // The token is recoloured through Context.Changed; the popup now describes the saved variable.
        if (_shown is { } token && Context is not null)
        {
            var refocus = _popupView?.IsKeyboardFocusWithin == true;
            Render(force: true);
            ShowPopup(_tokens.FirstOrDefault(t => t.Name == token.Name) ?? token, refocus);
        }
    }

    private void OnPopupCloseRequested(object? sender, EventArgs e)
    {
        ClosePopup();
        Input.Focus();
    }

    private void OnInputLostFocus(object? sender, RoutedEventArgs e) => CloseIfFocusLeft();

    private void CloseIfFocusLeft() =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_shown is not null && !_overPopup && _popupView?.IsKeyboardFocusWithin != true && !Input.IsKeyboardFocusWithin
                && _popupView?.HasOpenDropdown != true)
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
        _popup.IsOpen = false;
        _popup.Child = null;
        DetachPopup();
    }
}
