using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that converts a pasted Postman or Bruno script; <see cref="Result"/> holds the converted text once it is confirmed.</summary>
public sealed partial class ConvertScriptDialog : ContentDialog
{
    private readonly TextBox _source = new() { AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 140, MaxHeight = 200, FontFamily = new FontFamily("Consolas") };
    private readonly ComboBox _dialect = new() { Header = Strings.ScriptConvertDialect };
    private readonly TextBox _preview = new() { AcceptsReturn = true, IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, MinHeight = 140, MaxHeight = 200, FontFamily = new FontFamily("Consolas") };
    private readonly TextBlock _findings = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ScriptLanguage _language;
    private bool _syncing;

    /// <summary>Creates the dialog.</summary>
    /// <param name="language">The language of the script being edited.</param>
    public ConvertScriptDialog(ScriptLanguage language)
    {
        _language = language;
        Title = Strings.ScriptConvertTitle;
        PrimaryButtonText = Strings.ScriptConvertInsert;
        CloseButtonText = Strings.CommonCancel;
        DefaultButton = ContentDialogButton.Primary;
        IsPrimaryButtonEnabled = false;
        _source.Header = Strings.ScriptConvertSource;
        _preview.Header = Strings.ScriptConvertPreview;
        _dialect.Items.Add("Postman");
        _dialect.Items.Add("Bruno");
        _dialect.SelectedIndex = 0;
        _source.TextChanged += (_, _) => OnSourceChanged();
        _dialect.SelectionChanged += (_, _) => Refresh();
        PrimaryButtonClick += (_, _) => Result = _preview.Text;

        var panel = new StackPanel { Width = 520, Spacing = 8 };
        panel.Children.Add(_source);
        panel.Children.Add(_dialect);
        panel.Children.Add(_preview);
        panel.Children.Add(new TextBlock { Text = Strings.ScriptConvertFindings });
        panel.Children.Add(_findings);
        Content = new ScrollViewer { MaxHeight = 560, Content = panel };
    }

    /// <summary>Gets the converted script, or <see langword="null"/> when the dialog was not confirmed.</summary>
    public string? Result { get; private set; }

    // A paste picks the dialect it looks written for; the user can still override it.
    private void OnSourceChanged()
    {
        if (_source.Text.Length > 0)
        {
            _syncing = true;
            _dialect.SelectedIndex = (int)ScriptConverter.Detect(_source.Text);
            _syncing = false;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_syncing)
        {
            return;
        }

        var result = ScriptConverter.Convert(_source.Text, (ScriptDialect)Math.Max(_dialect.SelectedIndex, 0), _language);
        _preview.Text = result.Text;
        IsPrimaryButtonEnabled = _source.Text.Trim().Length > 0;
        _findings.Text = result.Findings.Count == 0
            ? Strings.ScriptConvertNone
            : string.Join('\n', result.Findings.Select(f => $"{Strings.ScriptConvertLine} {f.Line}: {f.Member} - {f.Reason}"));
    }
}
