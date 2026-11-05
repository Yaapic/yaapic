using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>Table of enabled/key/type/value rows bound to a <see cref="FormDataEditorViewModel"/>.</summary>
public sealed partial class FormDataEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(FormDataEditorViewModel), typeof(FormDataEditorView),
        new PropertyMetadata(null, (d, _) => ((FormDataEditorView)d).OnViewModelChanged()));

    /// <summary>Identifies the <see cref="VariableContext"/> dependency property.</summary>
    public static readonly DependencyProperty VariableContextProperty = DependencyProperty.Register(
        nameof(VariableContext), typeof(VariableContext), typeof(FormDataEditorView),
        new PropertyMetadata(null, (d, _) => ((FormDataEditorView)d).OnViewModelChanged()));

    /// <summary>Initializes the view.</summary>
    public FormDataEditorView() => InitializeComponent();

    /// <summary>Gets or sets the edited table.</summary>
    public FormDataEditorViewModel? ViewModel
    {
        get => (FormDataEditorViewModel?)GetValue(ViewModelProperty);
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
        if (((FrameworkElement)sender).Tag is FormDataRowViewModel row)
        {
            ViewModel?.RemoveRowCommand.Execute(row);
        }
    }

    private void OnKindChanged(object sender, SelectionChangedEventArgs e)
    {
        // The picker is index based: boxed managed enums cannot cross into XAML under Native AOT.
        var box = (ComboBox)sender;
        if (box.Tag is FormDataRowViewModel row && box.SelectedIndex >= 0)
        {
            row.Kind = box.SelectedIndex == 1 ? FormFieldKind.File : FormFieldKind.Text;
        }
    }
}
