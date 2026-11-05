using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;

namespace Yaapic.Domain.Tests;

public class ScriptModelTests
{
    private static readonly ScriptBlock Js = new(ScriptLanguage.JavaScript, "pm.test('a')");

    [Theory]
    [InlineData(ScriptLanguage.JavaScript, "javascript")]
    [InlineData(ScriptLanguage.Lua, "lua")]
    public void Language_text_round_trips(ScriptLanguage language, string text)
    {
        Assert.Equal(text, language.ToText());
        Assert.True(ScriptLanguageExtensions.TryParse(text, out var parsed));
        Assert.Equal(language, parsed);
    }

    [Theory]
    [InlineData("JavaScript", ScriptLanguage.JavaScript)]
    [InlineData("LUA", ScriptLanguage.Lua)]
    public void Language_parse_ignores_case(string text, ScriptLanguage expected)
    {
        Assert.True(ScriptLanguageExtensions.TryParse(text, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("python")]
    [InlineData(" lua")]
    public void Language_parse_rejects_unknown(string? text) =>
        Assert.False(ScriptLanguageExtensions.TryParse(text, out _));

    [Fact]
    public void Language_text_rejects_undefined() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ((ScriptLanguage)99).ToText());

    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("\r\n\n\t", true)]
    [InlineData("pm.test()", false)]
    public void Block_is_empty_follows_whitespace(string source, bool expected) =>
        Assert.Equal(expected, new ScriptBlock(ScriptLanguage.Lua, source).IsEmpty);

    [Fact]
    public void Empty_values_are_empty_and_equal()
    {
        Assert.True(ScriptBlock.Empty.IsEmpty);
        Assert.Equal(ScriptLanguage.JavaScript, ScriptBlock.Empty.Language);
        Assert.True(ScriptSet.Empty.IsEmpty);
        Assert.Equal(ScriptSet.Empty, new ScriptSet(ScriptBlock.Empty, ScriptBlock.Empty));
    }

    [Fact]
    public void Set_get_and_with_work_for_both_phases()
    {
        var lua = new ScriptBlock(ScriptLanguage.Lua, "print(1)");
        var set = ScriptSet.Empty.With(ScriptPhase.PreRequest, Js).With(ScriptPhase.PostResponse, lua);
        Assert.Equal(Js, set.Get(ScriptPhase.PreRequest));
        Assert.Equal(lua, set.Get(ScriptPhase.PostResponse));
        Assert.False(set.IsEmpty);
        Assert.False(ScriptSet.Empty.With(ScriptPhase.PostResponse, lua).IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Get((ScriptPhase)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => set.With((ScriptPhase)9, Js));
    }

    [Fact]
    public void Request_equivalence_sees_source_and_language()
    {
        var a = HttpRequestDefinition.Create("r");
        var withJs = a with { Scripts = ScriptSet.Empty.With(ScriptPhase.PreRequest, Js) };
        Assert.False(a.IsEquivalentTo(withJs));
        Assert.True(withJs.IsEquivalentTo(withJs with { }));
        Assert.False(withJs.IsEquivalentTo(withJs with { Scripts = withJs.Scripts.With(ScriptPhase.PreRequest, Js with { Source = "x" }) }));
        Assert.False(withJs.IsEquivalentTo(withJs with { Scripts = withJs.Scripts.With(ScriptPhase.PreRequest, Js with { Language = ScriptLanguage.Lua }) }));
    }

    [Fact]
    public void Folder_equivalence_sees_source_and_language()
    {
        var a = FolderDefinition.Create("f");
        var withJs = a with { Scripts = ScriptSet.Empty.With(ScriptPhase.PostResponse, Js) };
        Assert.False(a.IsEquivalentTo(withJs));
        Assert.True(withJs.IsEquivalentTo(withJs with { }));
        Assert.False(withJs.IsEquivalentTo(withJs with { Scripts = withJs.Scripts.With(ScriptPhase.PostResponse, Js with { Source = "x" }) }));
        Assert.False(withJs.IsEquivalentTo(withJs with { Scripts = withJs.Scripts.With(ScriptPhase.PostResponse, Js with { Language = ScriptLanguage.Lua }) }));
    }

    [Fact]
    public void Create_yields_empty_scripts()
    {
        Assert.Equal(ScriptSet.Empty, HttpRequestDefinition.Create("r").Scripts);
        Assert.Equal(ScriptSet.Empty, FolderDefinition.Create("f").Scripts);
    }

    [Fact]
    public void Omitted_scripts_default_to_empty()
    {
        var folder = new FolderDefinition(Guid.NewGuid(), "f", "", []);
        Assert.Equal(ScriptSet.Empty, folder.Scripts);
    }
}
