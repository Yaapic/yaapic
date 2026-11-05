using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class VariableContextTests
{
    private static readonly VariableScopes Sample = new(
        new EnvironmentDefinition(Guid.NewGuid(), "dev", [new KeyValueEntry("host", "api.test")]),
        [new KeyValueEntry("version", "v2")],
        [new KeyValueEntry("team", "core")]);

    private static (VariableContext Context, FakeVariableScopeProvider Scopes) Create()
    {
        var scopes = new FakeVariableScopeProvider { Scopes = Sample };
        return (new VariableContext(Guid.NewGuid(), scopes), scopes);
    }

    [Fact]
    public void TokenizeReportsOffsetsAndStates()
    {
        var (context, _) = Create();

        var tokens = context.Tokenize("https://{{host}}/{{ version }}/{{team}}/{{nope}}/{{$guid}}");

        Assert.Equal(
            [
                (8, 8, "host", VariableTokenState.Resolved, (VariableScopeKind?)VariableScopeKind.Environment),
                (17, 13, "version", VariableTokenState.Resolved, VariableScopeKind.Collection),
                (31, 8, "team", VariableTokenState.Resolved, VariableScopeKind.Globals),
                (40, 8, "nope", VariableTokenState.Unresolved, null),
                (49, 9, "$guid", VariableTokenState.Dynamic, VariableScopeKind.Dynamic),
            ],
            tokens.Select(t => (t.Start, t.Length, t.Name, t.State, t.Hint.Scope)));
        Assert.Equal("api.test", tokens[0].Hint.Value);
        Assert.Null(tokens[4].Hint.Value);
        Assert.Equal(16, tokens[0].End);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no variable { here }")]
    [InlineData("{{}}")]
    public void TokenizeFindsNothingInPlainText(string? text) => Assert.Empty(Create().Context.Tokenize(text));

    [Fact]
    public void ChangedIsRaisedOnlyWhileActive()
    {
        var (context, scopes) = Create();
        var raised = 0;
        context.Changed += (_, _) => raised++;

        scopes.RaiseScopesChanged();
        Assert.Equal(0, raised);

        context.IsActive = true;
        scopes.RaiseScopesChanged();
        Assert.Equal(1, raised);

        context.IsActive = false;
        scopes.RaiseScopesChanged();
        Assert.Equal(1, raised);
    }

    [Fact]
    public void EditorKeepsTheContextActiveWithTheTab()
    {
        var scopes = new FakeVariableScopeProvider { Scopes = Sample };
        var editor = RequestEditorViewModel.CreateNew(new RequestSender(new FakeHttpTransport((_, _) => throw new InvalidOperationException())), scopes);
        var raised = 0;
        editor.Variables.Changed += (_, _) => raised++;

        editor.IsActive = true;
        scopes.RaiseScopesChanged();
        editor.IsActive = false;
        scopes.RaiseScopesChanged();

        Assert.Equal(1, raised);
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData("$guid", false)]
    [InlineData("a}", false)]
    public void CreatableNames(string name, bool expected) => Assert.Equal(expected, VariableContext.IsCreatableName(name));
}
