using Yaapic.UI.Abstractions.Editing;

namespace Yaapic.ViewModels.Tests;

public sealed class ScriptApiCompletionTests
{
    private static string[] Names(string text, bool lua = false, bool explicitRequest = false) =>
        [.. ScriptApiCatalogue.Complete(text, lua, explicitRequest).Members.Select(m => m.NameIn(lua))];

    [Fact]
    public void Typing_a_word_offers_the_top_level_names()
    {
        Assert.Equal(["yaapic"], Names("y"));
        Assert.Equal(["console", "yaapic"], Names("", explicitRequest: true));
        Assert.Empty(Names(""));
    }

    [Fact]
    public void A_dot_offers_the_members_of_the_object()
    {
        Assert.Contains("environment", Names("yaapic."));
        Assert.Equal(["get", "has"], Names("yaapic.environment.ge").Concat(Names("yaapic.environment.ha")).ToArray());
        Assert.Contains("set", Names("yaapic.collectionVariables."));
    }

    [Fact]
    public void Only_the_last_line_matters()
    {
        Assert.Contains("test", Names("const a = 1;\nyaapic."));
    }

    [Fact]
    public void An_expect_call_continues_into_the_assertion_chain()
    {
        var names = Names("yaapic.expect(yaapic.response.code).to.be.");
        Assert.Contains("above", names);
        Assert.Contains("ok", names);
        Assert.Contains("above", Names("yaapic.expect(1).to.be.above(0).and."));
    }

    [Fact]
    public void Lua_gets_its_own_names()
    {
        Assert.Contains("collection_variables", Names("yaapic.", lua: true));
        Assert.DoesNotContain("collectionVariables", Names("yaapic.", lua: true));
        Assert.Contains("is_not", Names("yaapic.expect(1).to.", lua: true));
        Assert.Contains("set_url", Names("yaapic.request.", lua: true));
        Assert.DoesNotContain("setUrl", Names("yaapic.request."));
    }

    [Fact]
    public void An_unknown_object_offers_nothing()
    {
        Assert.Empty(Names("foo."));
        Assert.Empty(Names("yaapic.response.json()."));
    }
}
