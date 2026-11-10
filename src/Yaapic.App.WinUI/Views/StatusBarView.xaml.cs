using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.WinUI.Views;

/// <summary>The 24 px status bar: script engine warning, vault slot and the Console toggle with its counts.</summary>
public sealed partial class StatusBarView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ShellViewModel), typeof(StatusBarView),
        new PropertyMetadata(null, (d, _) => ((StatusBarView)d).Bindings.Update()));

    /// <summary>Initializes the view.</summary>
    public StatusBarView() => InitializeComponent();

    /// <summary>Gets or sets the shell shown.</summary>
    public ShellViewModel? ViewModel
    {
        get => (ShellViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }
}
