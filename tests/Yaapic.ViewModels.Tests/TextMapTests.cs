using Yaapic.App.WinUI.Presentation;
using Yaapic.Domain.Scripting;

namespace Yaapic.ViewModels.Tests;

public sealed class TextMapTests
{
    [Theory]
    [InlineData(ScriptLanguage.JavaScript, "javascript")]
    [InlineData(ScriptLanguage.Lua, "lua")]
    public void Script_language_maps_to_the_editor_language(ScriptLanguage language, string expected) =>
        Assert.Equal(expected, TextMap.ScriptLanguage(language));

    [Fact]
    public void First_line_is_the_first_entry_or_empty()
    {
        Assert.Equal("a", TextMap.FirstLine(["a", "b"]));
        Assert.Equal("", TextMap.FirstLine([]));
    }

    [Fact]
    public void Lines_are_joined_with_line_breaks() => Assert.Equal("a\nb", TextMap.JoinLines(["a", "b"]));
}
