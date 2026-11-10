using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>The hover card of a variable: its value and scope, or the form that creates it.</summary>
public sealed partial class VariablePopupView : UserControl
{
    private bool _loadingScopes;

    /// <summary>Initializes the card.</summary>
    /// <param name="viewModel">The variable under the pointer.</param>
    /// <param name="state">How the variable resolves, which picks the layout.</param>
    public VariablePopupView(VariablePopupViewModel viewModel, VariableTokenState state)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        State = state;
        InitializeComponent();
        LoadScopes();
    }

    /// <summary>Raised when the user dismisses the card with Escape.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets the variable shown.</summary>
    public VariablePopupViewModel ViewModel { get; }

    /// <summary>Gets how the variable resolves.</summary>
    public VariableTokenState State { get; }

    /// <summary>Gets the visibility of the header of a defined variable.</summary>
    public Visibility IsKnown => State == VariableTokenState.Unresolved ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Gets the visibility of the parts of a defined, stored variable.</summary>
    public Visibility IsResolvedState => State == VariableTokenState.Resolved ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the parts of a dynamic variable.</summary>
    public Visibility IsDynamicState => State == VariableTokenState.Dynamic ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the parts of an undefined variable.</summary>
    public Visibility IsUnresolvedState => State == VariableTokenState.Unresolved ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the lock glyph and the Reveal button of a secret variable.</summary>
    public Visibility IsSecretState => ViewModel.IsSecret ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the Edit button, hidden for a secret value.</summary>
    public Visibility IsEditable => ViewModel.IsSecret ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Gets the label of the Reveal button.</summary>
    /// <param name="isRevealed">Whether the value is shown.</param>
    /// <returns><c>Hide</c> while revealed, <c>Reveal</c> otherwise.</returns>
    public string RevealLabel(bool isRevealed) => isRevealed ? "Hide" : "Reveal";

    /// <summary>Gets the visibility of the explanation under the title.</summary>
    public Visibility HasMessage => ViewModel.Message.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Tells whether the read-only value is shown.</summary>
    /// <param name="isEditing">Whether the value is being edited.</param>
    /// <returns>Visible while not editing.</returns>
    public Visibility ShowValue(bool isEditing) => isEditing ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Tells whether the value editor is shown.</summary>
    /// <param name="isEditing">Whether the value is being edited.</param>
    /// <returns>Visible while editing.</returns>
    public Visibility ShowEditor(bool isEditing) => isEditing ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Tells whether the error line is shown.</summary>
    /// <param name="error">The last error.</param>
    /// <returns>Visible when there is an error.</returns>
    public Visibility ShowError(string? error) => string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Moves the keyboard focus to the first control of the card.</summary>
    public void FocusFirst()
    {
        var target = State == VariableTokenState.Unresolved ? (Control)ScopeBox : ViewModel.IsSecret ? RevealLink : EditLink;
        target.Focus(FocusState.Keyboard);
    }

    /// <summary>Tells whether the keyboard focus is inside the card.</summary>
    /// <returns><see langword="true"/> when a control of the card has focus.</returns>
    public bool ContainsFocus() => XamlRoot is not null && Contains(FocusManager.GetFocusedElement(XamlRoot) as DependencyObject);

    /// <summary>Tells whether an element is part of the card.</summary>
    /// <param name="element">The element to look for.</param>
    /// <returns><see langword="true"/> when the element is the card or one of its descendants.</returns>
    public bool Contains(DependencyObject? element)
    {
        for (; element is not null; element = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(element))
        {
            if (ReferenceEquals(element, this))
            {
                return true;
            }
        }

        return false;
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
                ToolTipService.SetToolTip(item, reason);
            }

            ScopeBox.Items.Add(item);
            if (choice.Kind == ViewModel.SelectedScope)
            {
                ScopeBox.SelectedIndex = i;
            }
        }

        _loadingScopes = false;
    }

    private void OnScopeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loadingScopes && ScopeBox.SelectedIndex >= 0)
        {
            ViewModel.SelectedScope = ViewModel.ScopeChoices[ScopeBox.SelectedIndex].Kind;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void OnValueKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.SubmitCommand.CanExecute(null))
        {
            ViewModel.SubmitCommand.Execute(null);
            e.Handled = true;
        }
    }
}
