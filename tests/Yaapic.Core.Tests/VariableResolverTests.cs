using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class VariableResolverTests
{
    static EnvironmentVariableSource Env(params (string K, string V, bool On)[] vars) =>
        new(EnvironmentDefinition.Create("e") with { Variables = [.. vars.Select(v => new KeyValueEntry(v.K, v.V, v.On))] });

    static ResolveResult R(string? s, params (string K, string V, bool On)[] vars) => VariableResolver.Resolve(s, Env(vars));

    [Fact]
    public void Replaces_single_and_multiple_placeholders()
    {
        Assert.Equal("a-1", R("a-{{x}}", ("x", "1", true)).Text);
        Assert.Equal("1/2/1", R("{{x}}/{{y}}/{{x}}", ("x", "1", true), ("y", "2", true)).Text);
    }

    [Fact]
    public void Trims_names() => Assert.Equal("1", R("{{  x }}", ("x", "1", true)).Text);

    [Fact]
    public void Unresolved_kept_as_typed_and_reported_once()
    {
        var r = R("{{ a }} {{a}} {{b}}");
        Assert.Equal("{{ a }} {{a}} {{b}}", r.Text);
        Assert.Equal(["a", "b"], r.Unresolved);
    }

    [Fact]
    public void Value_is_not_re_expanded() =>
        Assert.Equal("{{y}}", R("{{x}}", ("x", "{{y}}", true), ("y", "2", true)).Text);

    [Theory]
    [InlineData("{{}}")]
    [InlineData("{{ }}")]
    [InlineData("{{abc")]
    [InlineData("a{{b{c}}")]
    public void Malformed_left_untouched(string input)
    {
        var r = R(input, ("abc", "1", true));
        Assert.Equal(input, r.Text);
        Assert.Empty(r.Unresolved);
    }

    [Fact]
    public void Null_or_empty_input()
    {
        Assert.Equal("", R(null).Text);
        Assert.Equal("", R("").Text);
    }

    [Fact]
    public void Last_enabled_entry_wins_and_disabled_ignored()
    {
        Assert.Equal("3", R("{{x}}", ("x", "1", true), ("x", "3", true), ("x", "9", false)).Text);
        Assert.Equal("{{x}}", R("{{x}}", ("x", "1", false)).Text);
    }

    [Fact]
    public void Empty_key_ignored_and_case_sensitive() =>
        Assert.Equal("{{X}}", R("{{X}}", ("x", "1", true), ("", "2", true)).Text);

    [Fact]
    public void Null_environment_resolves_nothing()
    {
        var r = VariableResolver.Resolve("{{x}}", new EnvironmentVariableSource(null));
        Assert.Equal(["x"], r.Unresolved);
    }

    [Fact]
    public void Composite_first_hit_wins()
    {
        var c = new CompositeVariableSource(Env(("x", "1", true)), Env(("x", "2", true), ("y", "3", true)));
        Assert.Equal("1 3", VariableResolver.Resolve("{{x}} {{y}}", c).Text);
    }
}
