using Avalonia.Controls;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Import report (summary, severity filters, entries, Copy), bound to an <c>ImportReportViewModel</c> through its data context.</summary>
public sealed partial class ImportReportView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ImportReportView() => InitializeComponent();
}
