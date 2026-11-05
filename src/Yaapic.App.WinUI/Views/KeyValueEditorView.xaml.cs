using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>Table of enabled/key/value rows bound to a <see cref="KeyValueEditorViewModel"/>.</summary>
public sealed partial class KeyValueEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(KeyValueEditorViewModel), typeof(KeyValueEditorView),
        new PropertyMetadata(null, (d, _) => ((KeyValueEditorView)d).OnViewModelChanged()));

    /// <summary>Identifies the <see cref="KeyHeader"/> dependency property.</summary>
    public static readonly DependencyProperty KeyHeaderProperty = DependencyProperty.Register(
        nameof(KeyHeader), typeof(string), typeof(KeyValueEditorView), new PropertyMetadata("Key"));

    /// <summary>Identifies the <see cref="ValueHeader"/> dependency property.</summary>
    public static readonly DependencyProperty ValueHeaderProperty = DependencyProperty.Register(
        nameof(ValueHeader), typeof(string), typeof(KeyValueEditorView), new PropertyMetadata("Value"));

    /// <summary>Identifies the <see cref="VariableContext"/> dependency property.</summary>
    public static readonly DependencyProperty VariableContextProperty = DependencyProperty.Register(
        nameof(VariableContext), typeof(VariableContext), typeof(KeyValueEditorView),
        new PropertyMetadata(null, (d, _) => ((KeyValueEditorView)d).OnViewModelChanged()));

    /// <summary>Initializes the view.</summary>
    public KeyValueEditorView() => InitializeComponent();

    /// <summary>Gets or sets the key column header, shown in upper case.</summary>
    public string KeyHeader
    {
        get => (string)GetValue(KeyHeaderProperty);
        set => SetValue(KeyHeaderProperty, value);
    }

    /// <summary>Gets or sets the value column header, shown in upper case.</summary>
    public string ValueHeader
    {
        get => (string)GetValue(ValueHeaderProperty);
        set => SetValue(ValueHeaderProperty, value);
    }

    /// <summary>Gets or sets the edited table.</summary>
    public KeyValueEditorViewModel? ViewModel
    {
        get => (KeyValueEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Gets or sets the context that colours <c>{{variables}}</c> in the cells; <see langword="null"/> keeps them plain.</summary>
    public VariableContext? VariableContext
    {
        get => (VariableContext?)GetValue(VariableContextProperty);
        set => SetValue(VariableContextProperty, value);
    }

    // Rebinding recreates the rows, so every realised cell picks up the new context.
    private void OnViewModelChanged()
    {
        RowList.ItemsSource = null;
        RowList.ItemsSource = ViewModel?.Rows;
    }

    private void OnRowPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args) =>
        VariableTextBox.ApplyContext(args.Element, VariableContext);

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is KeyValueRowViewModel row)
        {
            ViewModel?.RemoveRowCommand.Execute(row);
        }
    }
}
