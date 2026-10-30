using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Editing;

namespace Yaapic.App.WinUI.Views;

/// <summary>Table of enabled/key/value rows bound to a <see cref="KeyValueEditorViewModel"/>.</summary>
public sealed partial class KeyValueEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(KeyValueEditorViewModel), typeof(KeyValueEditorView),
        new PropertyMetadata(null, (d, _) => ((KeyValueEditorView)d).OnViewModelChanged()));

    /// <summary>Initializes the view.</summary>
    public KeyValueEditorView() => InitializeComponent();

    /// <summary>Gets or sets the edited table.</summary>
    public KeyValueEditorViewModel? ViewModel
    {
        get => (KeyValueEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged() => RowList.ItemsSource = ViewModel?.Rows;

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is KeyValueRowViewModel row)
        {
            ViewModel?.RemoveRowCommand.Execute(row);
        }
    }
}
