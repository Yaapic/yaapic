using Yaapic.App.Avalonia.Presentation;
using Yaapic.Domain.Scripting;

namespace Yaapic.ViewModels.Tests;

public sealed class EditorMapTests
{
    [Theory]
    [InlineData(ScriptLanguage.JavaScript, "javascript")]
    [InlineData(ScriptLanguage.Lua, "lua")]
    public void Script_language_maps_to_the_editor_language(ScriptLanguage language, string expected) =>
        Assert.Equal(expected, EditorMap.ScriptLanguage(language));
}
