using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorVariableTokensTests
{
    private static readonly VariableScopes Sample = new(
        new EnvironmentDefinition(Guid.NewGuid(), "dev", [new KeyValueEntry("host", "api.test")]),
        [new KeyValueEntry("version", "v2")],
        [new KeyValueEntry("team", "core")]);

    private static (RequestEditorViewModel Editor, FakeVariableScopeProvider Scopes) Create()
    {
        var scopes = new FakeVariableScopeProvider { Scopes = Sample };
        var editor = RequestEditorViewModel.CreateNew(new RequestSender(new FakeHttpTransport((_, _) => throw new InvalidOperationException())), scopes);
        editor.IsActive = true;
        return (editor, scopes);
    }

    [Fact]
    public void TokensCoverEveryScopeUnknownAndDynamic()
    {
        var (editor, _) = Create();

        var tokens = editor.Variables.Tokenize("https://{{host}}/{{version}}/{{team}}/{{nope}}/{{$guid}}");

        Assert.Collection(
            tokens,
            t => Assert.Equal(new VariableHint("host", true, "api.test", VariableScopeKind.Environment), t.Hint),
            t => Assert.Equal(new VariableHint("version", true, "v2", VariableScopeKind.Collection), t.Hint),
            t => Assert.Equal(new VariableHint("team", true, "core", VariableScopeKind.Globals), t.Hint),
            t => Assert.Equal(new VariableHint("nope", false, null, null), t.Hint),
            t => Assert.Equal(new VariableHint("$guid", true, null, VariableScopeKind.Dynamic), t.Hint));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://x.test/plain")]
    public void NoVariablesMeansNoTokens(string url)
    {
        var (editor, _) = Create();

        Assert.Empty(editor.Variables.Tokenize(url));
    }

    [Fact]
    public void TokensFollowScopeChangesAndNotifyWhileActive()
    {
        var (editor, scopes) = Create();
        var changed = 0;
        editor.Variables.Changed += (_, _) => changed++;
        editor.Url = "{{host}}";

        scopes.Scopes = new VariableScopes(null, [], []);
        scopes.RaiseScopesChanged();

        Assert.Equal(1, changed);
        Assert.False(Assert.Single(editor.Variables.Tokenize(editor.Url)).Hint.IsResolved);
    }

    [Fact]
    public void ScopeChangesAreIgnoredWhileInactive()
    {
        var (editor, scopes) = Create();
        var changed = 0;
        editor.Variables.Changed += (_, _) => changed++;
        editor.IsActive = false;

        scopes.RaiseScopesChanged();
        Assert.Equal(0, changed);

        var activated = 0;
        editor.Variables.Activated += (_, _) => activated++;
        editor.IsActive = true;
        Assert.Equal(1, activated);
        scopes.RaiseScopesChanged();
        Assert.Equal(1, changed);
    }
}
