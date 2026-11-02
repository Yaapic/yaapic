using Avalonia.Controls;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Editing;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Form-data table bound to a <see cref="FormDataEditorViewModel"/>.</summary>
public sealed partial class FormDataEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public FormDataEditorView() => InitializeComponent();

    private void OnKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { Tag: FormDataRowViewModel row, SelectedIndex: >= 0 } box)
        {
            row.Kind = box.SelectedIndex == 1 ? FormFieldKind.File : FormFieldKind.Text;
        }
    }
}
