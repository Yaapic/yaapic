using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Editing;

namespace Yaapic.App.WinUI.Views;

/// <summary>Table of enabled/key/type/value rows bound to a <see cref="FormDataEditorViewModel"/>.</summary>
public sealed partial class FormDataEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(FormDataEditorViewModel), typeof(FormDataEditorView),
        new PropertyMetadata(null, (d, _) => ((FormDataEditorView)d).OnViewModelChanged()));

    /// <summary>Initializes the view.</summary>
    public FormDataEditorView() => InitializeComponent();

    /// <summary>Gets or sets the edited table.</summary>
    public FormDataEditorViewModel? ViewModel
    {
        get => (FormDataEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged() => RowList.ItemsSource = ViewModel?.Rows;

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
