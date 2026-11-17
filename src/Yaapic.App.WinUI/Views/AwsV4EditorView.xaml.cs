using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>AWS Signature Version 4 editor bound to an <see cref="AwsV4EditorViewModel"/>.</summary>
public sealed partial class AwsV4EditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(AwsV4EditorViewModel), typeof(AwsV4EditorView),
        new PropertyMetadata(null, (d, _) => ((AwsV4EditorView)d).Bindings?.Update()));

    /// <summary>Initializes the view.</summary>
    public AwsV4EditorView() => InitializeComponent();

    /// <summary>Gets or sets the edited AWS Signature v4 configuration.</summary>
    public AwsV4EditorViewModel? ViewModel
    {
        get => (AwsV4EditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
