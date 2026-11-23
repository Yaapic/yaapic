using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Import;

namespace Yaapic.App.WinUI.Views;

/// <summary>Import report (summary, severity filters, entries, Copy) bound to an <see cref="ImportReportViewModel"/>.</summary>
public sealed partial class ImportReportView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ImportReportViewModel), typeof(ImportReportView),
        new PropertyMetadata(null, (d, _) => ((ImportReportView)d).Bindings?.Update()));

    /// <summary>Initializes the view.</summary>
    public ImportReportView() => InitializeComponent();

    /// <summary>Gets or sets the report view model, set by the host dialog.</summary>
    public ImportReportViewModel? ViewModel
    {
        get => (ImportReportViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
