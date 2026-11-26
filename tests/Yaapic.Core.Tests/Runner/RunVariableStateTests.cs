using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.Runner;

public class RunVariableStateTests
{
    private static VariableChange Set(VariableScopeKind s, string n, string v) => new(s, n, v);

    private static VariableChange Unset(VariableScopeKind s, string n) => new(s, n, null);

    private static VariableScopes Scopes(params KeyValueEntry[] env) =>
        new(new EnvironmentDefinition(Guid.NewGuid(), "e", env), [new("c", "1")], [new("g", "1")]);

    [Fact]
    public void Starts_empty_and_ignores_other_scopes()
    {
        var state = new RunVariableState();
        Assert.True(state.IsEmpty);
        state.Apply([Set(VariableScopeKind.Local, "x", "1"), Set(VariableScopeKind.Data, "y", "1")]);
        Assert.True(state.IsEmpty);
    }

    [Fact]
    public void Set_unset_set_keeps_the_last_state()
    {
        var state = new RunVariableState();
        state.Apply([Set(VariableScopeKind.Environment, "a", "1"), Unset(VariableScopeKind.Environment, "a"), Set(VariableScopeKind.Environment, "a", "2")]);
        Assert.False(state.IsEmpty);
        var result = state.Overlay(Scopes(), IterationDataRow.Empty);
        Assert.Equal([new KeyValueEntry("a", "2")], result.Environment!.Variables);
    }

    [Fact]
    public void Applies_changes_to_each_scope()
    {
        var state = new RunVariableState();
        state.Apply([Set(VariableScopeKind.Collection, "c", "2"), Unset(VariableScopeKind.Globals, "g"), Set(VariableScopeKind.Environment, "e", "x")]);
        var result = state.Overlay(Scopes(), IterationDataRow.Empty);
        Assert.Equal([new KeyValueEntry("c", "2")], result.Collection);
        Assert.Empty(result.Globals);
        Assert.Equal([new KeyValueEntry("e", "x")], result.Environment!.Variables);
    }

    [Fact]
    public void Drops_environment_changes_when_there_is_no_environment()
    {
        var state = new RunVariableState();
        state.Apply([Set(VariableScopeKind.Environment, "a", "1")]);
        var result = state.Overlay(VariableScopes.None, IterationDataRow.Empty);
        Assert.Null(result.Environment);
    }

    [Fact]
    public void Updating_a_secret_row_keeps_the_flag()
    {
        var state = new RunVariableState();
        state.Apply([Set(VariableScopeKind.Environment, "s", "new")]);
        var result = state.Overlay(Scopes(new KeyValueEntry("s", "old", true, true)), IterationDataRow.Empty);
        Assert.Equal([new KeyValueEntry("s", "new", true, true)], result.Environment!.Variables);
    }

    [Fact]
    public void A_disabled_row_of_the_same_name_gets_a_new_enabled_row()
    {
        var state = new RunVariableState();
        state.Apply([Set(VariableScopeKind.Environment, "a", "2")]);
        var result = state.Overlay(Scopes(new KeyValueEntry("a", "1", false)), IterationDataRow.Empty);
        Assert.Equal([new KeyValueEntry("a", "1", false), new KeyValueEntry("a", "2")], result.Environment!.Variables);
    }

    [Fact]
    public void Unset_removes_every_row_of_the_name()
    {
        var state = new RunVariableState();
        state.Apply([Unset(VariableScopeKind.Environment, "a")]);
        var result = state.Overlay(Scopes(new("a", "1"), new("a", "2", false), new("b", "3")), IterationDataRow.Empty);
        Assert.Equal([new KeyValueEntry("b", "3")], result.Environment!.Variables);
    }

    [Fact]
    public void Data_comes_from_the_row_and_the_input_is_not_mutated()
    {
        var state = new RunVariableState();
        state.Apply([Set(VariableScopeKind.Collection, "c", "9")]);
        var input = Scopes();
        var row = new IterationDataRow([new("d", "v")]);
        var result = state.Overlay(input, row);
        Assert.Equal(row.Values, result.Data);
        Assert.Equal([new KeyValueEntry("c", "1")], input.Collection);
        Assert.Null(input.Data);
    }
}
