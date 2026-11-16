using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that converts a pasted Postman or Bruno script and closes with the converted text, or <see langword="null"/>.</summary>
public sealed class ConvertScriptWindow : Window
{
    private readonly TextBox _source = new() { AcceptsReturn = true, MinHeight = 140, MaxHeight = 200, FontFamily = new("Consolas,Menlo,monospace") };
    private readonly ComboBox _dialect = new() { ItemsSource = new[] { "Postman", "Bruno" }, SelectedIndex = 0 };
    private readonly TextBox _preview = new() { AcceptsReturn = true, IsReadOnly = true, MinHeight = 140, MaxHeight = 200, FontFamily = new("Consolas,Menlo,monospace") };
    private readonly TextBlock _findings = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Button _insert = new() { Content = Strings.ScriptConvertInsert, IsEnabled = false, IsDefault = true };
    private readonly ScriptLanguage _language;
    private bool _syncing;

    /// <summary>Creates the window.</summary>
    /// <param name="language">The language of the script being edited.</param>
    public ConvertScriptWindow(ScriptLanguage language)
    {
        _language = language;
        Title = Strings.ScriptConvertTitle;
        Classes.Add("dialog");
        _insert.Classes.Add("accent");
        var cancel = new Button { Content = Strings.CommonCancel, IsCancel = true };
        _insert.Click += (_, _) => Close(_preview.Text);
        cancel.Click += (_, _) => Close(null);
        _source.TextChanged += (_, _) => OnSourceChanged();
        _dialect.SelectionChanged += (_, _) => Refresh();

        var body = new StackPanel { Margin = new(24, 24, 24, 20), Spacing = 8 };
        body.Children.Add(Label(Strings.ScriptConvertSource));
        body.Children.Add(_source);
        body.Children.Add(Label(Strings.ScriptConvertDialect));
        body.Children.Add(_dialect);
        body.Children.Add(Label(Strings.ScriptConvertPreview));
        body.Children.Add(_preview);
        body.Children.Add(Label(Strings.ScriptConvertFindings));
        body.Children.Add(_findings);

        var commands = new Border { Child = new UniformGrid { Rows = 1, Children = { _insert, cancel } } };
        commands.Classes.Add("dialogCommands");
        Width = 560;
        Content = new StackPanel { Children = { new ScrollViewer { MaxHeight = 640, Content = body }, commands } };
    }

    private static TextBlock Label(string text)
    {
        var label = new TextBlock { Text = text };
        label.Classes.Add("title");
        return label;
    }

    // A paste picks the dialect it looks written for; the user can still override it.
    private void OnSourceChanged()
    {
        var text = _source.Text ?? "";
        if (text.Length > 0)
        {
            _syncing = true;
            _dialect.SelectedIndex = (int)ScriptConverter.Detect(text);
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

        var text = _source.Text ?? "";
        var result = ScriptConverter.Convert(text, (ScriptDialect)Math.Max(_dialect.SelectedIndex, 0), _language);
        _preview.Text = result.Text;
        _insert.IsEnabled = text.Trim().Length > 0;
        _findings.Text = result.Findings.Count == 0
            ? Strings.ScriptConvertNone
            : string.Join('\n', result.Findings.Select(f => $"{Strings.ScriptConvertLine} {f.Line}: {f.Member} - {f.Reason}"));
    }
}
