using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Domain.Scripting;
using Yaapic.ViewModels.Scripting;

namespace Yaapic.App.WinUI.Views;

/// <summary>Phase switch, language picker and code editor of a <see cref="ScriptsEditorViewModel"/>.</summary>
public sealed partial class ScriptsEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ScriptsEditorViewModel), typeof(ScriptsEditorView),
        new PropertyMetadata(null, (d, _) => ((ScriptsEditorView)d).OnViewModelChanged()));

    /// <summary>Identifies the <see cref="HintText"/> dependency property.</summary>
    public static readonly DependencyProperty HintTextProperty = DependencyProperty.Register(
        nameof(HintText), typeof(string), typeof(ScriptsEditorView),
        new PropertyMetadata("Variables, tests and requests: pm.* (JavaScript and Lua)", (d, e) => ((ScriptsEditorView)d).Hint.Text = (string)e.NewValue));

    private bool _syncing;

    /// <summary>Initializes the view.</summary>
    public ScriptsEditorView()
    {
        InitializeComponent();
        Hint.Text = HintText;
        foreach (var choice in ScriptBlockEditorViewModel.Languages)
        {
            ScriptLanguageBox.Items.Add(new ComboBoxItem { Content = choice.Label });
        }
    }

    /// <summary>Gets or sets the edited scripts.</summary>
    public ScriptsEditorViewModel? ViewModel
    {
        get => (ScriptsEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>Gets or sets the one-line hint shown next to the pickers.</summary>
    public string HintText
    {
        get => (string)GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    private void OnViewModelChanged()
    {
        Bindings?.Update();
        SyncPickers();
    }

    // Both pickers are index based (enums cannot cross into XAML under Native AOT) and follow the selected block.
    private void SyncPickers()
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        _syncing = true;
        try
        {
            ScriptPhases.SelectedIndex = vm.SelectedPhase == ScriptPhase.PostResponse ? 1 : 0;
            ScriptLanguageBox.SelectedIndex = LanguageIndex(vm.Selected.Language);
        }
        finally
        {
            _syncing = false;
        }
    }

    private static int LanguageIndex(ScriptLanguage language)
    {
        for (var i = 0; i < ScriptBlockEditorViewModel.Languages.Count; i++)
        {
            if (ScriptBlockEditorViewModel.Languages[i].Language == language)
            {
                return i;
            }
        }

        return 0;
    }

    private void OnScriptPhaseChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && ViewModel is { } vm && ScriptPhases.SelectedIndex >= 0)
        {
            vm.SelectedPhase = ScriptPhases.SelectedIndex == 1 ? ScriptPhase.PostResponse : ScriptPhase.PreRequest;
            // The editor now shows the other block (x:Bind re-targets Selected); its undo history starts over.
            SyncPickers();
        }
    }

    private void OnScriptLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncing && ViewModel is { } vm && ScriptLanguageBox.SelectedIndex >= 0)
        {
            vm.Selected.Language = ScriptBlockEditorViewModel.Languages[ScriptLanguageBox.SelectedIndex].Language;
        }
    }
}
