using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class ScriptVariableOverlayTests
{
    static ScriptVariableOverlay Make(bool env = true, bool col = true) => new(
        new VariableScopes(
            EnvironmentDefinition.Create("e") with { Variables = [new("a", "env"), new("off", "old", false)] },
            [new("a", "col"), new("b", "col")],
            [new("a", "glob"), new("b", "glob"), new("c", "glob")]),
        env,
        col);

    [Fact]
    public void Layered_precedence_is_local_environment_collection_globals_dynamic()
    {
        var o = Make();
        Assert.True(o.TryGet(ScriptScope.Layered, "a", out var v));
        Assert.Equal("env", v);
        Assert.True(o.TryGet(ScriptScope.Layered, "b", out v));
        Assert.Equal("col", v);
        Assert.True(o.TryGet(ScriptScope.Layered, "c", out v));
        Assert.Equal("glob", v);
        o.Set(ScriptScope.Layered, "a", "loc");
        Assert.True(o.TryGet(ScriptScope.Layered, "a", out v));
        Assert.Equal("loc", v);
        Assert.True(o.TryGet(ScriptScope.Layered, "$guid", out v));
        Assert.True(Guid.TryParse(v, out _));
        Assert.False(o.TryGet(ScriptScope.Globals, "$guid", out _));
        Assert.DoesNotContain("$guid", o.List(ScriptScope.Layered).Keys);
    }

    [Theory]
    [InlineData(ScriptScope.Environment, "env")]
    [InlineData(ScriptScope.Collection, "col")]
    [InlineData(ScriptScope.Globals, "glob")]
    public void Scoped_reads_see_only_that_layer(ScriptScope scope, string expected)
    {
        var o = Make();
        Assert.True(o.TryGet(scope, "a", out var v));
        Assert.Equal(expected, v);
        Assert.False(o.TryGet(scope, "zzz", out _));
        Assert.False(o.TryGet(ScriptScope.Local, "a", out _));
        Assert.False(o.TryGet(ScriptScope.Environment, "off", out _));
    }

    [Theory]
    [InlineData(ScriptScope.Environment, VariableScopeKind.Environment)]
    [InlineData(ScriptScope.Collection, VariableScopeKind.Collection)]
    [InlineData(ScriptScope.Globals, VariableScopeKind.Globals)]
    public void Writes_to_persisted_scopes_are_tracked(ScriptScope scope, VariableScopeKind kind)
    {
        var o = Make();
        o.Set(scope, "n", "1");
        Assert.True(o.TryGet(scope, "n", out var v));
        Assert.Equal("1", v);
        Assert.Equal(new VariableChange(kind, "n", "1"), Assert.Single(o.Changes));
        Assert.Empty(o.Warnings);
    }

    [Fact]
    public void Local_and_layered_writes_are_not_tracked()
    {
        var o = Make();
        o.Set(ScriptScope.Local, "x", "1");
        o.Set(ScriptScope.Layered, "y", "2");
        Assert.True(o.TryGet(ScriptScope.Local, "y", out _));
        Assert.Empty(o.Changes);
    }

    [Fact]
    public void Upsert_updates_existing_and_enables_disabled_rows()
    {
        var o = Make();
        o.Set(ScriptScope.Environment, "a", "new");
        o.Set(ScriptScope.Environment, "off", "on");
        var scopes = o.ToScopes();
        Assert.Equal(2, scopes.Environment!.Variables.Count);
        Assert.Contains(new KeyValueEntry("off", "on"), scopes.Environment.Variables);
        Assert.Contains(new KeyValueEntry("a", "new"), scopes.Environment.Variables);
    }

    [Fact]
    public void Missing_scopes_fall_back_to_local_with_a_warning_and_no_change()
    {
        var o = Make(env: false, col: false);
        o.Set(ScriptScope.Environment, "x", "1");
        o.Set(ScriptScope.Collection, "y", "2");
        Assert.Empty(o.Changes);
        Assert.Equal(2, o.Warnings.Count);
        Assert.Contains("No active environment", o.Warnings[0]);
        Assert.True(o.TryGet(ScriptScope.Local, "x", out _));
        Assert.True(o.TryGet(ScriptScope.Local, "y", out _));
    }

    [Fact]
    public void Repeated_writes_collapse_keeping_first_write_order()
    {
        var o = Make();
        o.Set(ScriptScope.Globals, "p", "1");
        o.Set(ScriptScope.Globals, "q", "1");
        o.Set(ScriptScope.Globals, "p", "2");
        Assert.Equal(
            [new VariableChange(VariableScopeKind.Globals, "p", "2"), new VariableChange(VariableScopeKind.Globals, "q", "1")],
            o.Changes);
    }

    [Fact]
    public void Unset_and_clear_record_unsets_for_persisted_scopes_only()
    {
        var o = Make();
        o.Set(ScriptScope.Local, "l", "1");
        o.Unset(ScriptScope.Local, "l");
        o.Unset(ScriptScope.Globals, "c");
        o.Unset(ScriptScope.Globals, "missing");
        var unset = Assert.Single(o.Changes);
        Assert.True(unset.IsUnset);
        Assert.Equal("c", unset.Name);

        o.Clear(ScriptScope.Collection);
        o.Set(ScriptScope.Local, "z", "1");
        o.Clear(ScriptScope.Local);
        Assert.Empty(o.List(ScriptScope.Local));
        Assert.Empty(o.List(ScriptScope.Collection));
        Assert.Equal(3, o.Changes.Count);
        Assert.All(o.Changes, c => Assert.True(c.IsUnset));

        o.Clear(ScriptScope.Environment); // only the enabled row "a"; "off" is disabled
        Assert.Equal(4, o.Changes.Count);
        Assert.Equal(new VariableChange(VariableScopeKind.Environment, "a", null), o.Changes[^1]);
    }

    [Fact]
    public void ReplaceIn_resolves_local_environment_dynamic_and_keeps_unresolved()
    {
        var o = Make();
        o.Set(ScriptScope.Local, "l", "L");
        var text = o.ReplaceIn("{{ l }}|{{a}}|{{$guid}}|{{nope}}");
        var parts = text.Split('|');
        Assert.Equal("L", parts[0]);
        Assert.Equal("env", parts[1]);
        Assert.True(Guid.TryParse(parts[2], out _));
        Assert.Equal("{{nope}}", parts[3]);
    }

    [Fact]
    public void ToScopes_gives_local_the_highest_priority()
    {
        var o = Make();
        o.Set(ScriptScope.Local, "a", "loc");
        var scopes = o.ToScopes();
        Assert.True(scopes.TryResolve("a", out var v, out var kind));
        Assert.Equal("loc", v);
        Assert.Equal(VariableScopeKind.Local, kind);
        Assert.True(scopes.CreateSource().TryGet("a", out v));
        Assert.Equal("loc", v);
    }
}
