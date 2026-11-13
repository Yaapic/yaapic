using Avalonia.Controls;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;
using Yaapic.UI.Abstractions.Editing;
using Yaapic.ViewModels.Scripting;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Phase switch, language picker and code editor of a <see cref="ScriptsEditorViewModel"/>.</summary>
public sealed partial class ScriptsEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ScriptsEditorView()
    {
        InitializeComponent();
        foreach (var choice in ScriptBlockEditorViewModel.Languages)
        {
            ScriptLanguageBox.Items.Add(new ComboBoxItem { Content = choice.Label });
        }

        ScriptEditor.SnippetPhaseSource = CurrentPhase;
    }

    private ScriptsEditorViewModel? Scripts => DataContext as ScriptsEditorViewModel;

    private ScriptSnippetPhase CurrentPhase() =>
        Scripts?.SelectedPhase == ScriptPhase.PostResponse ? ScriptSnippetPhase.PostResponse : ScriptSnippetPhase.PreRequest;

    // The menu lists the snippets of the language and phase being edited; a pick is inserted at the caret.
    private void OnSnippetsOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout flyout || Scripts is not { } scripts)
        {
            return;
        }

        flyout.Items.Clear();
        var lua = scripts.Selected.Language == ScriptLanguage.Lua;
        foreach (var snippet in ScriptSnippetCatalogue.For(lua, CurrentPhase()))
        {
            var template = snippet.Template(lua)!;
            var item = new MenuItem { Header = snippet.Name };
            item.Click += (_, _) => ScriptEditor.InsertSnippet(template);
            flyout.Items.Add(item);
        }

        if (flyout.Items.Count == 0)
        {
            flyout.Items.Add(new MenuItem { Header = Strings.ScriptSnippetsEmpty, IsEnabled = false });
        }
    }

    // Index based, like the body kind: the position is the one in the picker items.
    private void OnScriptPhaseChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Scripts is { } scripts && ScriptPhases.SelectedIndex >= 0)
        {
            scripts.SelectedPhase = ScriptPhases.SelectedIndex == 1 ? ScriptPhase.PostResponse : ScriptPhase.PreRequest;
        }
    }

    private void OnScriptLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Scripts is { } scripts && ScriptLanguageBox.SelectedIndex >= 0)
        {
            scripts.Selected.Language = ScriptBlockEditorViewModel.Languages[ScriptLanguageBox.SelectedIndex].Language;
        }
    }
}
