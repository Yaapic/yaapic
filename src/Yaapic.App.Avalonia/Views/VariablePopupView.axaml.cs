using Avalonia.Controls;
using Avalonia.Input;
using Yaapic.App.Avalonia.Presentation;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The hover card of a variable: its value and scope, or the form that creates it.</summary>
public sealed partial class VariablePopupView : UserControl
{
    private bool _loadingScopes;

    /// <summary>Creates an empty card; <see cref="Load"/> fills it.</summary>
    public VariablePopupView() => InitializeComponent();

    /// <summary>Shows a variable.</summary>
    /// <param name="viewModel">The variable under the pointer.</param>
    /// <param name="state">How the variable resolves, which picks the layout.</param>
    /// <param name="canOpen">Whether the editor of the variable's scope can be opened from here.</param>
    public void Load(VariablePopupViewModel viewModel, VariableTokenState state, bool canOpen)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        DataContext = viewModel;
        State = state;
        var unresolved = state == VariableTokenState.Unresolved;
        KnownHeader.IsVisible = !unresolved;
        UnresolvedHeader.IsVisible = unresolved;
        ResolvedPanel.IsVisible = state == VariableTokenState.Resolved;
        UnresolvedPanel.IsVisible = unresolved;
        MessageText.IsVisible = viewModel.Message.Length > 0;
        OpenLink.IsVisible = canOpen;
        LoadScopes();
    }

    /// <summary>Raised when the user dismisses the card with Escape.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets how the variable resolves.</summary>
    public VariableTokenState State { get; private set; }

    /// <summary>Gets a value indicating whether a dropdown of the card is open; the card must then stay open.</summary>
    public bool HasOpenDropdown => ScopeBox.IsDropDownOpen;

    private VariablePopupViewModel ViewModel => (VariablePopupViewModel)DataContext!;

    /// <summary>Moves the keyboard focus to the first control of the card.</summary>
    public void FocusFirst() => (State == VariableTokenState.Unresolved ? (Control)ScopeBox : EditLink).Focus(NavigationMethod.Tab);

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The token colour depends on the theme variant, which is only known once the card is in a tree.
        NameText.Foreground = BrushMap.Token(this, State).Foreground;
    }

    // The picker is index based: boxed managed enums cannot cross into XAML under Native AOT.
    private void LoadScopes()
    {
        _loadingScopes = true;
        for (var i = 0; i < ViewModel.ScopeChoices.Count; i++)
        {
            var choice = ViewModel.ScopeChoices[i];
            var item = new ComboBoxItem
            {
                Content = choice.IsEnabled ? choice.Label : $"{choice.Label} ({choice.DisabledReason})",
                IsEnabled = choice.IsEnabled,
            };
            if (choice.DisabledReason is { } reason)
            {
                ToolTip.SetTip(item, reason);
            }

            ScopeBox.Items.Add(item);
            if (choice.Kind == ViewModel.SelectedScope)
            {
                ScopeBox.SelectedIndex = i;
            }
        }

        _loadingScopes = false;
    }

    private void OnScopeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loadingScopes && ScopeBox.SelectedIndex >= 0)
        {
            ViewModel.SelectedScope = ViewModel.ScopeChoices[ScopeBox.SelectedIndex].Kind;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void OnValueKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel.SubmitCommand.CanExecute(null))
        {
            ViewModel.SubmitCommand.Execute(null);
            e.Handled = true;
        }
    }
}
