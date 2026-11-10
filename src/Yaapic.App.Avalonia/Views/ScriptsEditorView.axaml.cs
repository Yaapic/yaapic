using Avalonia.Controls;
using Yaapic.Domain.Scripting;
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
    }

    private ScriptsEditorViewModel? Scripts => DataContext as ScriptsEditorViewModel;

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
