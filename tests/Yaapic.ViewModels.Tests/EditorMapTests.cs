using Yaapic.App.Avalonia.Presentation;
using Yaapic.Domain.Scripting;
using Yaapic.ViewModels.Scripting;

namespace Yaapic.ViewModels.Tests;

public sealed class EditorMapTests
{
    [Theory]
    [InlineData(ScriptLanguage.JavaScript, "javascript")]
    [InlineData(ScriptLanguage.Lua, "lua")]
    public void Script_language_maps_to_the_editor_language(ScriptLanguage language, string expected) =>
        Assert.Equal(expected, EditorMap.ScriptLanguage(language));

    [Theory]
    [InlineData(ScriptPhase.PreRequest, 0)]
    [InlineData(ScriptPhase.PostResponse, 1)]
    public void Script_phase_maps_to_its_switch_index(ScriptPhase phase, int expected) =>
        Assert.Equal(expected, EditorMap.ScriptPhaseIndex(phase));

    [Theory]
    [InlineData(ScriptLanguage.JavaScript, 0)]
    [InlineData(ScriptLanguage.Lua, 1)]
    public void Script_language_maps_to_its_picker_index_in_the_view_model_order(ScriptLanguage language, int expected)
    {
        Assert.Equal(language, ScriptBlockEditorViewModel.Languages[expected].Language);
        Assert.Equal(expected, EditorMap.ScriptLanguageIndex(language));
    }
}
