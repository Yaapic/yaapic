using Yaapic.UI.Abstractions.Editing;

namespace Yaapic.ViewModels.Tests;

public sealed class ScriptApiCompletionTests
{
    private static string[] Names(string text, bool lua = false, bool explicitRequest = false) =>
        [.. ScriptApiCatalogue.Complete(text, lua, explicitRequest).Members.Select(m => m.NameIn(lua))];

    [Fact]
    public void Typing_a_word_offers_the_top_level_names()
    {
        Assert.Equal(["pm"], Names("p"));
        Assert.Equal(["console", "pm", "yaapic"], Names("", explicitRequest: true));
        Assert.Empty(Names(""));
    }

    [Fact]
    public void A_dot_offers_the_members_of_the_object()
    {
        Assert.Contains("environment", Names("pm."));
        Assert.Equal(["get", "has"], Names("pm.environment.ge").Concat(Names("pm.environment.ha")).ToArray());
        Assert.Contains("set", Names("pm.collectionVariables."));
    }

    [Fact]
    public void Only_the_last_line_matters()
    {
        Assert.Contains("test", Names("const a = 1;\npm."));
    }

    [Fact]
    public void An_expect_call_continues_into_the_assertion_chain()
    {
        var names = Names("pm.expect(pm.response.code).to.be.");
        Assert.Contains("above", names);
        Assert.Contains("ok", names);
        Assert.Contains("above", Names("pm.expect(1).to.be.above(0).and."));
    }

    [Fact]
    public void Lua_gets_its_own_names()
    {
        Assert.Contains("collection_variables", Names("pm.", lua: true));
        Assert.DoesNotContain("collectionVariables", Names("pm.", lua: true));
        Assert.Contains("is_not", Names("pm.expect(1).to.", lua: true));
        Assert.Contains("set_url", Names("pm.request.", lua: true));
        Assert.DoesNotContain("setUrl", Names("pm.request."));
    }

    [Fact]
    public void An_unknown_object_offers_nothing()
    {
        Assert.Empty(Names("foo."));
        Assert.Empty(Names("pm.response.json()."));
    }
}
