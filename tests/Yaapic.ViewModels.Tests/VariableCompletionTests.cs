using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class VariableCompletionTests
{
    [Theory]
    [InlineData("https://{{", 10, 8, "")]
    [InlineData("https://{{ho", 12, 8, "ho")]
    [InlineData("{{a}}/{{b", 9, 6, "b")]
    [InlineData("{{ ho", 5, 0, "ho")]
    public void FindsTheOpenPlaceholder(string text, int caret, int start, string prefix)
    {
        Assert.True(VariableCompletion.TryGetPrefix(text, caret, out var s, out var p));
        Assert.Equal((start, prefix), (s, p));
    }

    [Theory]
    [InlineData("https://host", 12)]
    [InlineData("{{a}}", 5)]
    [InlineData("{a", 2)]
    [InlineData("{{a}} b", 7)]
    [InlineData("", 0)]
    public void IgnoresACaretOutsideAPlaceholder(string text, int caret) =>
        Assert.False(VariableCompletion.TryGetPrefix(text, caret, out _, out _));

    [Fact]
    public void AcceptingClosesThePlaceholderAndMovesAfterIt()
    {
        var (text, caret) = VariableCompletion.Apply("{{ho/path", 4, 0, "host");
        Assert.Equal(("{{host}}/path", 8), (text, caret));
    }

    [Fact]
    public void AcceptingReusesTheClosingBraces()
    {
        var (text, caret) = VariableCompletion.Apply("{{ho}}/x", 4, 0, "host");
        Assert.Equal(("{{host}}/x", 8), (text, caret));
    }

    private static VariableContext Context(FakeVariableScopeProvider provider) => new(Guid.NewGuid(), provider);

    [Fact]
    public void SuggestsEachNameOnceFromTheWinningScopeThenOtherEnvironments()
    {
        var provider = new FakeVariableScopeProvider
        {
            Scopes = new VariableScopes(
                new EnvironmentDefinition(Guid.NewGuid(), "local", [new("host", "localhost"), new("token", "secret", true, true)]),
                [new("host", "collection-host"), new("version", "v1")],
                [new("version", "g"), new("user", "me")]),
            OtherEnvironments = [new EnvironmentDefinition(Guid.NewGuid(), "staging", [new("host", "x"), new("stagingOnly", "1")])],
        };

        var all = Context(provider).Suggest("");

        Assert.Equal(1, all.Count(s => s.Name == "host"));
        Assert.Equal(VariableScopeKind.Environment, all.Single(s => s.Name == "host").Scope);
        Assert.Equal("", all.Single(s => s.Name == "token").Preview);
        Assert.Contains(all, s => s.Name == "$guid" && s.Scope == VariableScopeKind.Dynamic);
        var inactive = all.Single(s => s.Name == "stagingOnly");
        Assert.False(inactive.IsActive);
        Assert.Contains("staging", inactive.ScopeLabel);
    }

    [Fact]
    public void FiltersByPrefixWithPrefixMatchesFirst()
    {
        var provider = new FakeVariableScopeProvider
        {
            Scopes = new VariableScopes(null, [], [new("apiHost", "1"), new("host", "2"), new("other", "3")]),
        };

        var found = Context(provider).Suggest("ho").Select(s => s.Name).ToArray();

        Assert.Equal(["host", "apiHost"], found);
    }
}
