using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Yaapic.App.WinUI.Presentation;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>Editor of one request tab: name, method, URL, send, params, headers and body.</summary>
public sealed partial class RequestEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RequestEditorViewModel), typeof(RequestEditorView),
        new PropertyMetadata(null, (d, _) => ((RequestEditorView)d).OnViewModelChanged()));

    /// <summary>Raised when the user asks to save the request from the header button.</summary>
    public event RoutedEventHandler? SaveRequested;

    /// <summary>Raised when a variable popup asks to open the editor of the scope that holds the variable.</summary>
    public event EventHandler<VariableScopeOpenRequest>? VariableScopeOpenRequested;

    /// <summary>Initializes the view.</summary>
    public RequestEditorView()
    {
        InitializeComponent();
        foreach (var method in RequestEditorViewModel.Methods)
        {
            var item = new MenuFlyoutItem
            {
                Text = method.ToText(),
                Tag = method.ToText(),
                Foreground = BrushMap.Method(method),
                FontFamily = (FontFamily)Application.Current.Resources["YaapicMonoFont"],
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                FontSize = 12,
            };
            item.Click += OnMethodClick;
            MethodFlyout.Items.Add(item);
        }

        // The brushes depend on the window theme, which can change after construction: refresh them whenever the menu opens.
        MethodFlyout.Opening += (_, _) =>
        {
            for (var i = 0; i < RequestEditorViewModel.Methods.Count; i++)
            {
                ((MenuFlyoutItem)MethodFlyout.Items[i]).Foreground = BrushMap.Method(RequestEditorViewModel.Methods[i]);
            }
        };
        ActualThemeChanged += (_, _) =>
        {
            BrushMap.IsLight = ActualTheme == ElementTheme.Light;
            Bindings?.Update();
        };
    }

    private static readonly string[] ContentTypeNames = ["JSON", "Text", "XML", "HTML", "JavaScript"];

    /// <summary>Gets or sets the edited request.</summary>
    public RequestEditorViewModel? ViewModel
    {
        get => (RequestEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private VariableContext? _variables;

    private PaneLayoutViewModel? _layout;

    private void OnViewModelChanged()
    {
        Bindings?.Update();
        if (_layout is not null)
        {
            _layout.PropertyChanged -= OnLayoutChanged;
        }

        _layout = ViewModel?.Layout;
        if (_layout is not null)
        {
            _layout.PropertyChanged += OnLayoutChanged;
        }

        ApplyLayout();
        if (_variables is not null)
        {
            _variables.OpenRequested -= OnVariableOpenRequested;
        }

        _variables = ViewModel?.Variables;
        if (_variables is not null)
        {
            _variables.OpenRequested += OnVariableOpenRequested;
        }

        if (ViewModel is not { } vm)
        {
            return;
        }

        // The pickers are index based: boxed managed enums cannot cross into XAML under Native AOT.
        BodyNone.IsChecked = vm.BodyKind == BodyKind.None;
        BodyRaw.IsChecked = vm.BodyKind == BodyKind.Raw;
        BodyUrlEncoded.IsChecked = vm.BodyKind == BodyKind.UrlEncoded;
        BodyFormData.IsChecked = vm.BodyKind == BodyKind.FormData;
        BodyBinary.IsChecked = vm.BodyKind == BodyKind.Binary;
        ContentTypeLabel.Text = ContentTypeNames[IndexOf(RequestEditorViewModel.ContentTypes, vm.BodyContentType)];
        TimeoutBox.Value = vm.TimeoutSeconds;
    }

    private void OnVariableOpenRequested(object? sender, Yaapic.Core.Variables.VariableScopeKind scope)
    {
        if (ViewModel is { } vm)
        {
            VariableScopeOpenRequested?.Invoke(this, new VariableScopeOpenRequest(vm.Id, scope));
        }
    }

    private void OnTimeoutChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (ViewModel is not null)
        {
            // An emptied box reports NaN; it means "use the default".
            ViewModel.TimeoutSeconds = double.IsNaN(args.NewValue) ? 0 : (int)args.NewValue;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, e);

    private void OnCancelAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Escape only cancels a send in flight; otherwise it stays available to the focused control.
        if (ViewModel is { IsSending: true } vm && vm.CancelCommand.CanExecute(null))
        {
            vm.CancelCommand.Execute(null);
            args.Handled = true;
        }
    }

    private void OnUrlBarGotFocus(object sender, RoutedEventArgs e) => SetUrlBarFocused(true);

    private void OnUrlBarLostFocus(object sender, RoutedEventArgs e) => SetUrlBarFocused(false);

    private void SetUrlBarFocused(bool focused)
    {
        // A 2 px focus border replaces the 1 px one; the negative content margin keeps the text from moving.
        UrlBar.BorderBrush = BrushMap.Themed(focused ? "YaapicFocusRingBrush" : "YaapicBorderControlBrush");
        UrlBar.BorderThickness = new Thickness(focused ? 2 : 1);
        UrlBarContent.Margin = new Thickness(focused ? -1 : 0);
    }

    private void OnMethodClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            // The flyout is built from Methods in order, so the item index is the method index.
            ViewModel.Method = RequestEditorViewModel.Methods[MethodFlyout.Items.IndexOf((MenuFlyoutItem)sender)];
        }
    }

    private void OnContentTypeClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            var index = int.Parse((string)((MenuFlyoutItem)sender).Tag, System.Globalization.CultureInfo.InvariantCulture);
            ViewModel.BodyContentType = RequestEditorViewModel.ContentTypes[index];
            ContentTypeLabel.Text = ContentTypeNames[index];
        }
    }

    private void OnBodyKindChecked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.BodyKind = ReferenceEquals(sender, BodyRaw) ? BodyKind.Raw
                : ReferenceEquals(sender, BodyUrlEncoded) ? BodyKind.UrlEncoded
                : ReferenceEquals(sender, BodyFormData) ? BodyKind.FormData
                : ReferenceEquals(sender, BodyBinary) ? BodyKind.Binary
                : BodyKind.None;
        }
    }

    private void OnSendAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel?.SendCommand.CanExecute(null) == true)
        {
            ViewModel.SendCommand.Execute(null);
        }

        args.Handled = true;
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        ParamsTab.IsChecked = ReferenceEquals(sender, ParamsTab);
        HeadersTab.IsChecked = ReferenceEquals(sender, HeadersTab);
        BodyTab.IsChecked = ReferenceEquals(sender, BodyTab);
        AuthTab.IsChecked = ReferenceEquals(sender, AuthTab);
        SettingsTab.IsChecked = ReferenceEquals(sender, SettingsTab);
        ScriptsTab.IsChecked = ReferenceEquals(sender, ScriptsTab);
        ScriptsPane.Visibility = ScriptsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ScriptsBadge.Style = BadgeStyle(ScriptsTab);
        AuthPane.Visibility = AuthTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SettingsPane.Visibility = SettingsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AuthBadge.Style = BadgeStyle(AuthTab);
        ParamsPane.Visibility = ParamsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        HeadersPane.Visibility = HeadersTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        BodyPane.Visibility = BodyTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ParamsBadge.Style = BadgeStyle(ParamsTab);
        HeadersBadge.Style = BadgeStyle(HeadersTab);
        BodyBadge.Style = BadgeStyle(BodyTab);
    }

    private bool Horizontal => _layout?.IsHorizontal == true;

    private void OnLayoutChanged(object? sender, PropertyChangedEventArgs e) => ApplyLayout();

    // Stacked: everything spans the three columns and the response is in row 5. Side by side: the header and URL bar keep the full width,
    // the section tabs and editor take the left column, and the splitter and response sit in the other two columns next to them.
    private void ApplyLayout()
    {
        var horizontal = Horizontal;
        LayoutIcon.Icon = horizontal ? FluentIcons.Common.Icon.SplitHorizontal : FluentIcons.Common.Icon.SplitVertical;
        var span = horizontal ? 1 : 3;
        Grid.SetColumnSpan(SectionTabs, span);
        Grid.SetColumnSpan(EditorContent, span);
        RequestColumn.Width = new GridLength(1, GridUnitType.Star);
        RequestColumn.MinWidth = horizontal ? 280 : 0;
        SplitterColumn.Width = horizontal ? GridLength.Auto : new GridLength(0);
        ResponseColumn.Width = horizontal ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        ResponseColumn.MinWidth = horizontal ? 280 : 0;
        ResponseRow.MinHeight = horizontal ? 0 : 120;
        Grid.SetRow(ResponseView, horizontal ? 2 : 5);
        Grid.SetRowSpan(ResponseView, horizontal ? 2 : 1);
        Grid.SetColumn(ResponseView, horizontal ? 2 : 0);
        Grid.SetColumnSpan(ResponseView, horizontal ? 1 : 3);
        Grid.SetRow(SplitterHost, horizontal ? 2 : 4);
        Grid.SetRowSpan(SplitterHost, horizontal ? 2 : 1);
        Grid.SetColumn(SplitterHost, horizontal ? 1 : 0);
        Grid.SetColumnSpan(SplitterHost, horizontal ? 1 : 3);
        SplitterHost.Height = horizontal ? double.NaN : 5;
        SplitterHost.Width = horizontal ? 5 : double.NaN;
        Splitter.SetVertical(horizontal);
        ResetSplitter();
        UpdateSplitterLine();
    }

    private const double SplitterStep = 16;
    private bool _splitterDragging;

    private double _splitterLast;

    private void OnSplitterPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Splitter.CapturePointer(e.Pointer))
        {
            _splitterDragging = true;
            _splitterLast = Position(e);
            Splitter.Focus(FocusState.Pointer);
            UpdateSplitterLine();
        }
    }

    private void OnSplitterPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_splitterDragging)
        {
            var position = Position(e);
            MoveSplitter(position - _splitterLast);
            _splitterLast = position;
        }
    }

    private double Position(PointerRoutedEventArgs e) => Horizontal ? e.GetCurrentPoint(this).Position.X : e.GetCurrentPoint(this).Position.Y;

    private void OnSplitterPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _splitterDragging = false;
        Splitter.ReleasePointerCaptures();
        UpdateSplitterLine();
    }

    private void OnSplitterDoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => ResetSplitter();

    private void OnSplitterKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Up when !Horizontal:
            case Windows.System.VirtualKey.Left when Horizontal:
                MoveSplitter(-SplitterStep);
                break;
            case Windows.System.VirtualKey.Down when !Horizontal:
            case Windows.System.VirtualKey.Right when Horizontal:
                MoveSplitter(SplitterStep);
                break;
            case Windows.System.VirtualKey.Home: ResetSplitter(); break;
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
        var thickness = _splitterDragging ? 2 : 1;
        SplitterLine.Height = Horizontal ? double.NaN : thickness;
        SplitterLine.Width = Horizontal ? thickness : double.NaN;
        SplitterLine.HorizontalAlignment = Horizontal ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        SplitterLine.VerticalAlignment = Horizontal ? VerticalAlignment.Stretch : VerticalAlignment.Center;
        SplitterLine.Fill = BrushMap.Themed(active ? "YaapicAccentFillBrush" : "YaapicBorderSubtleBrush");
    }

    private void MoveSplitter(double delta)
    {
        // The request side switches from a star to a pixel size; the response side keeps the rest.
        if (Horizontal)
        {
            var total = RequestColumn.ActualWidth + ResponseColumn.ActualWidth;
            RequestColumn.Width = new GridLength(Math.Clamp(RequestColumn.ActualWidth + delta, RequestColumn.MinWidth, Math.Max(RequestColumn.MinWidth, total - ResponseColumn.MinWidth)));
            ResponseColumn.Width = new GridLength(1, GridUnitType.Star);
            return;
        }

        var totalHeight = RequestRow.ActualHeight + ResponseRow.ActualHeight;
        var height = Math.Clamp(RequestRow.ActualHeight + delta, RequestRow.MinHeight, Math.Max(RequestRow.MinHeight, totalHeight - ResponseRow.MinHeight));
        RequestRow.Height = new GridLength(height);
        ResponseRow.Height = new GridLength(1, GridUnitType.Star);
    }

    private void ResetSplitter()
    {
        if (Horizontal)
        {
            RequestColumn.Width = new GridLength(1, GridUnitType.Star);
            ResponseColumn.Width = new GridLength(1, GridUnitType.Star);
            RequestRow.Height = new GridLength(1, GridUnitType.Star);
            ResponseRow.Height = new GridLength(0);
            return;
        }

        RequestRow.Height = new GridLength(3, GridUnitType.Star);
        ResponseRow.Height = new GridLength(2, GridUnitType.Star);
    }

    private static Style BadgeStyle(ToggleButton tab) =>
        (Style)Application.Current.Resources[tab.IsChecked == true ? "YaapicCountBadgeActiveTextStyle" : "YaapicCountBadgeTextStyle"];

    private static int IndexOf<T>(IReadOnlyList<T> items, T value)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(items[i], value))
            {
                return i;
            }
        }

        return 0;
    }
}

/// <summary>A request to open the editor of a variable scope.</summary>
/// <param name="RequestId">The request whose variable was shown.</param>
/// <param name="Scope">The scope to open.</param>
public sealed record VariableScopeOpenRequest(Guid RequestId, Yaapic.Core.Variables.VariableScopeKind Scope);
