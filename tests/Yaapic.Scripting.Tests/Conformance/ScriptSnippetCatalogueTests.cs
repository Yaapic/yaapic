using Yaapic.Domain.Scripting;
using Yaapic.UI.Abstractions.Editing;
using static Yaapic.Scripting.Tests.Conformance.ConformanceRunner;

namespace Yaapic.Scripting.Tests.Conformance;

/// <summary>Runs every built-in script snippet, with its default text, on the real engines: none may raise a script error.</summary>
public sealed class ScriptSnippetCatalogueTests
{
    public static TheoryData<string, bool> Cases()
    {
        TheoryData<string, bool> data = [];
        foreach (var snippet in ScriptSnippetCatalogue.All)
        {
            data.Add(snippet.Prefix, false);
            data.Add(snippet.Prefix, true);
        }

        return data;
    }

    [Fact]
    public void Each_language_has_at_least_ten_snippets()
    {
        Assert.True(ScriptSnippetCatalogue.All.Count(s => s.JavaScript is not null) >= 10);
        Assert.True(ScriptSnippetCatalogue.All.Count(s => s.Lua is not null) >= 10);
    }

    [Fact]
    public void Prefixes_are_unique()
    {
        Assert.Equal(ScriptSnippetCatalogue.All.Count, ScriptSnippetCatalogue.All.Select(s => s.Prefix).Distinct().Count());
    }

    [Fact]
    public void For_filters_by_language_and_phase()
    {
        var lua = ScriptSnippetCatalogue.For(lua: true, ScriptSnippetPhase.PreRequest);
        Assert.DoesNotContain(lua, s => s.Prefix == "preBody");
        Assert.DoesNotContain(lua, s => s.Prefix == "testStatus");
        Assert.Contains(lua, s => s.Prefix == "setVar");
        Assert.Contains(ScriptSnippetCatalogue.For(lua: false, ScriptSnippetPhase.PostResponse), s => s.Prefix == "testStatus");
        Assert.Contains(ScriptSnippetCatalogue.For(lua: false, ScriptSnippetPhase.Any), s => s.Prefix == "preBody");
    }

    [Fact]
    public void PlainText_keeps_defaults_and_drops_bare_tab_stops()
    {
        Assert.Equal("a(\"x\", 1)", ScriptSnippet.PlainText("a(\"${1:x}\", ${2:1})$0"));
        Assert.Equal("{{token}}", ScriptSnippet.PlainText("{{${1:token}}}"));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Snippet_runs_without_error(string prefix, bool lua)
    {
        var snippet = ScriptSnippetCatalogue.All.Single(s => s.Prefix == prefix);
        if (snippet.Template(lua) is not { } template)
        {
            return;
        }

        var script = ScriptSnippet.PlainText(template);
        var pre = snippet.Phase != ScriptSnippetPhase.PostResponse ? script : null;
        var post = snippet.Phase != ScriptSnippetPhase.PreRequest ? script : null;

        var (report, _) = await Run(new($"snippet-{prefix}") { Language = lua ? ScriptLanguage.Lua : ScriptLanguage.JavaScript, Env = ["token=abc"] }, pre, post);

        Assert.All(report.Runs, r => Assert.True(r.Succeeded, r.Error?.Message));
        // A test inside a snippet must also pass against the stub response.
        Assert.All(report.Tests, t => Assert.True(t.Passed, $"{t.Name}: {t.Message}"));
    }
}
