using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorUrlVariablesTests
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
    public void HintsCoverEveryScopeUnknownAndDynamic()
    {
        var (editor, _) = Create();

        editor.Url = "https://{{host}}/{{version}}/{{team}}/{{nope}}/{{$guid}}";

        Assert.True(editor.HasUrlVariables);
        Assert.Collection(
            editor.UrlVariables,
            h => Assert.Equal(new VariableHint("host", true, "api.test", VariableScopeKind.Environment), h),
            h => Assert.Equal(new VariableHint("version", true, "v2", VariableScopeKind.Collection), h),
            h => Assert.Equal(new VariableHint("team", true, "core", VariableScopeKind.Globals), h),
            h => Assert.Equal(new VariableHint("nope", false, null, null), h),
            h => Assert.Equal(new VariableHint("$guid", true, null, VariableScopeKind.Dynamic), h));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://x.test/plain")]
    public void NoVariablesMeansNoHints(string url)
    {
        var (editor, _) = Create();

        editor.Url = url;

        Assert.False(editor.HasUrlVariables);
        Assert.Empty(editor.UrlVariables);
    }

    [Fact]
    public void HintsUpdateWhenScopesChangeWhileActive()
    {
        var (editor, scopes) = Create();
        editor.Url = "{{host}}";

        scopes.Scopes = new VariableScopes(null, [], []);
        scopes.RaiseScopesChanged();

        Assert.False(Assert.Single(editor.UrlVariables).IsResolved);
    }

    [Fact]
    public void HintsIgnoreScopeChangesWhileInactiveAndRefreshOnActivation()
    {
        var (editor, scopes) = Create();
        editor.Url = "{{host}}";
        editor.IsActive = false;
        Assert.Empty(editor.UrlVariables);

        scopes.Scopes = new VariableScopes(null, [], []);
        scopes.RaiseScopesChanged();
        Assert.Empty(editor.UrlVariables);

        editor.IsActive = true;
        Assert.False(Assert.Single(editor.UrlVariables).IsResolved);
    }
}
