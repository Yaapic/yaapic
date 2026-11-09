using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Scripting;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorScriptsTests
{
    private static RequestEditorViewModel Editor(ScriptSet? scripts = null)
    {
        var sender = new RequestSender(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "unused"))));
        var definition = HttpRequestDefinition.Create("Scripted") with { Scripts = scripts ?? ScriptSet.Empty };
        return RequestEditorViewModel.FromDefinition(definition, sender, new FakeVariableScopeProvider());
    }

    public static TheoryData<ScriptSet> Sets => new()
    {
        ScriptSet.Empty,
        new ScriptSet(new ScriptBlock(ScriptLanguage.JavaScript, "pm.test('a')"), new ScriptBlock(ScriptLanguage.Lua, "pm.test('b')")),
        new ScriptSet(new ScriptBlock(ScriptLanguage.Lua, "print(1)"), new ScriptBlock(ScriptLanguage.JavaScript, "")),
    };

    public static TheoryData<ScriptLanguage, ScriptPhase, string> Placeholders => new()
    {
        { ScriptLanguage.JavaScript, ScriptPhase.PreRequest, "// Runs before the request is sent.\n// pm.environment.set(\"requestId\", pm.variables.replaceIn(\"{{$guid}}\"));" },
        { ScriptLanguage.JavaScript, ScriptPhase.PostResponse, "// Runs after the response arrives.\n// pm.test(\"Status is 200\", () => pm.response.to.have.status(200));" },
        { ScriptLanguage.Lua, ScriptPhase.PreRequest, "-- Runs before the request is sent.\n-- pm.environment.set(\"requestId\", pm.variables.replace_in(\"{{$guid}}\"))" },
        { ScriptLanguage.Lua, ScriptPhase.PostResponse, "-- Runs after the response arrives.\n-- pm.test(\"Status is 200\", function() pm.response.to.have.status(200) end)" },
    };

    [Theory]
    [MemberData(nameof(Sets))]
    public void LoadThenToSetRoundTrips(ScriptSet set)
    {
        var scripts = new ScriptsEditorViewModel();
        scripts.Load(set);

        Assert.Equal(set, scripts.ToSet());
    }

    [Fact]
    public void LoadDoesNotRaiseChanged()
    {
        var scripts = new ScriptsEditorViewModel();
        var raised = false;
        scripts.Changed += (_, _) => raised = true;

        scripts.Load(new ScriptSet(new ScriptBlock(ScriptLanguage.Lua, "x"), ScriptBlock.Empty));

        Assert.False(raised);
    }

    [Fact]
    public void SourceEditMarksDirtyAndMarkSavedClears()
    {
        var editor = Editor();
        Assert.False(editor.IsDirty);

        editor.Scripts.PostResponse.Source = "pm.test('x')";

        Assert.True(editor.IsDirty);
        Assert.EndsWith(" •", editor.Title);
        editor.MarkSaved();
        Assert.False(editor.IsDirty);
        Assert.DoesNotContain("•", editor.Title);
    }

    [Fact]
    public void LanguageChangeKeepsSourceAndMarksDirty()
    {
        var editor = Editor(new ScriptSet(new ScriptBlock(ScriptLanguage.JavaScript, "code"), ScriptBlock.Empty));

        editor.Scripts.PreRequest.Language = ScriptLanguage.Lua;

        Assert.Equal("code", editor.Scripts.PreRequest.Source);
        Assert.True(editor.IsDirty);
        Assert.Contains("•", editor.Title);
    }

    [Fact]
    public void BadgeCountsFilledBlocksAndIgnoresWhitespace()
    {
        var scripts = new ScriptsEditorViewModel();
        Assert.Equal((0, ""), (scripts.FilledCount, scripts.BadgeText));

        scripts.PreRequest.Source = "  \n\t";
        Assert.Equal((0, ""), (scripts.FilledCount, scripts.BadgeText));

        scripts.PreRequest.Source = "a";
        Assert.Equal((1, "1"), (scripts.FilledCount, scripts.BadgeText));

        scripts.PostResponse.Source = "b";
        Assert.Equal((2, "2"), (scripts.FilledCount, scripts.BadgeText));
    }

    [Fact]
    public void EditorRaisesScriptsBadge()
    {
        var editor = Editor();
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        editor.Scripts.PreRequest.Source = "a";

        Assert.Contains(nameof(RequestEditorViewModel.ScriptsBadge), changed);
        Assert.Equal("1", editor.ScriptsBadge);
    }

    [Theory]
    [MemberData(nameof(Placeholders))]
    public void PlaceholderDependsOnLanguageAndPhase(ScriptLanguage language, ScriptPhase phase, string expected)
    {
        var block = new ScriptBlockEditorViewModel(phase) { Language = language };

        Assert.Equal(expected, block.Placeholder);
    }

    [Fact]
    public void LanguageChangeRaisesPlaceholder()
    {
        var block = new ScriptBlockEditorViewModel(ScriptPhase.PreRequest);
        var changed = new List<string?>();
        block.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        block.Language = ScriptLanguage.Lua;

        Assert.Contains(nameof(ScriptBlockEditorViewModel.Placeholder), changed);
    }

    [Fact]
    public void OffersJavaScriptAndLua()
    {
        Assert.Equal(
            [ScriptLanguage.JavaScript, ScriptLanguage.Lua],
            ScriptBlockEditorViewModel.Languages.Select(c => c.Language));
    }

    [Fact]
    public void SelectedPhaseSwitchesSelected()
    {
        var scripts = new ScriptsEditorViewModel();
        Assert.Same(scripts.PreRequest, scripts.Selected);

        scripts.SelectedPhase = ScriptPhase.PostResponse;

        Assert.Same(scripts.PostResponse, scripts.Selected);
    }

    [Fact]
    public async Task ToDefinitionCarriesScriptsThroughTheStore()
    {
        var set = new ScriptSet(new ScriptBlock(ScriptLanguage.Lua, "a"), new ScriptBlock(ScriptLanguage.JavaScript, "b"));
        var editor = Editor();
        editor.Scripts.Load(set);
        Assert.Equal(set, editor.ToDefinition().Scripts);

        var store = new InMemoryRequestStore();
        await store.SaveAsync(editor.ToDefinition(), TestContext.Current.CancellationToken);
        var saved = Assert.Single(await store.ListAsync(TestContext.Current.CancellationToken));
        var reloaded = RequestEditorViewModel.FromDefinition(
            saved,
            new RequestSender(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "unused")))),
            new FakeVariableScopeProvider());

        Assert.Equal(set, reloaded.Scripts.ToSet());
        Assert.False(reloaded.IsDirty);
    }
}
