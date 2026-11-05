using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class VariableScopesTests
{
    static VariableScopes All() => new(
        EnvironmentDefinition.Create("e") with { Variables = [new("a", "env")] },
        [new("a", "col"), new("b", "col")],
        [new("a", "glob"), new("b", "glob"), new("c", "glob")]);

    [Theory]
    [InlineData("a", "env", VariableScopeKind.Environment)]
    [InlineData("b", "col", VariableScopeKind.Collection)]
    [InlineData("c", "glob", VariableScopeKind.Globals)]
    public void Resolves_by_precedence_and_reports_scope(string name, string value, VariableScopeKind kind)
    {
        var scopes = All();
        Assert.True(scopes.TryResolve(name, out var v, out var k));
        Assert.Equal(value, v);
        Assert.Equal(kind, k);
        Assert.True(scopes.CreateSource().TryGet(name, out var sv));
        Assert.Equal(value, sv);
    }

    [Fact]
    public void Dynamic_variable_reports_dynamic_scope()
    {
        Assert.True(VariableScopes.None.TryResolve("$guid", out var v, out var k));
        Assert.True(Guid.TryParse(v, out _));
        Assert.Equal(VariableScopeKind.Dynamic, k);
    }

    [Fact]
    public void Unknown_name_is_not_resolved()
    {
        Assert.False(All().TryResolve("zzz", out var v, out _));
        Assert.Equal("", v);
        Assert.False(All().CreateSource().TryGet("zzz", out _));
    }

    [Fact]
    public void Disabled_and_empty_key_entries_are_ignored()
    {
        var scopes = new VariableScopes(null, [new("a", "x", false), new("", "y")], [new("a", "g")]);
        Assert.True(scopes.TryResolve("a", out var v, out var k));
        Assert.Equal("g", v);
        Assert.Equal(VariableScopeKind.Globals, k);
        Assert.False(scopes.TryResolve("", out _, out _));
    }
}
