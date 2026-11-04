using Avalonia;
using Avalonia.Controls;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Key/value table bound to a <see cref="Yaapic.ViewModels.Editing.KeyValueEditorViewModel"/>.</summary>
public sealed partial class KeyValueEditorView : UserControl
{
    /// <summary>Identifies <see cref="KeyHeader"/>.</summary>
    public static readonly StyledProperty<string> KeyHeaderProperty =
        AvaloniaProperty.Register<KeyValueEditorView, string>(nameof(KeyHeader), "KEY");

    /// <summary>Identifies <see cref="ValueHeader"/>.</summary>
    public static readonly StyledProperty<string> ValueHeaderProperty =
        AvaloniaProperty.Register<KeyValueEditorView, string>(nameof(ValueHeader), "VALUE");

    /// <summary>Identifies <see cref="VariableContext"/>.</summary>
    public static readonly StyledProperty<VariableContext?> VariableContextProperty =
        AvaloniaProperty.Register<KeyValueEditorView, VariableContext?>(nameof(VariableContext));

    /// <summary>Creates the view.</summary>
    public KeyValueEditorView() => InitializeComponent();

    /// <summary>Gets or sets the caption of the key column.</summary>
    public string KeyHeader
    {
        get => GetValue(KeyHeaderProperty);
        set => SetValue(KeyHeaderProperty, value);
    }

    /// <summary>Gets or sets the caption of the value column.</summary>
    public string ValueHeader
    {
        get => GetValue(ValueHeaderProperty);
        set => SetValue(ValueHeaderProperty, value);
    }

    /// <summary>Gets or sets the context that colours <c>{{variable}}</c> tokens in the cells; <see langword="null"/> keeps plain cells.</summary>
    public VariableContext? VariableContext
    {
        get => GetValue(VariableContextProperty);
        set => SetValue(VariableContextProperty, value);
    }
}
