using Yaapic.Domain.Environments;

namespace Yaapic.Domain.Tests;

public class EnvironmentDefinitionTests
{
    [Fact]
    public void Create_trims_name()
    {
        var e = EnvironmentDefinition.Create("  Dev ");
        Assert.Equal("Dev", e.Name);
        Assert.Empty(e.Variables);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_rejects_blank_name(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => EnvironmentDefinition.Create(name!));

    static EnvironmentDefinition Sample() =>
        EnvironmentDefinition.Create("a") with { Variables = [new("x", "1"), new("y", "2")] };

    [Fact]
    public void IsEquivalentTo_true_for_equal_content_in_different_lists()
    {
        var a = Sample();
        Assert.True(a.IsEquivalentTo(a with { Variables = [new("x", "1"), new("y", "2")] }));
    }

    public static TheoryData<Func<EnvironmentDefinition, EnvironmentDefinition>> Changes => new()
    {
        e => e with { Name = "b" },
        e => e with { Variables = [new("x", "9"), new("y", "2")] },
        e => e with { Variables = [new("y", "2"), new("x", "1")] },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void IsEquivalentTo_false_after_change(Func<EnvironmentDefinition, EnvironmentDefinition> change)
    {
        var a = Sample();
        Assert.False(a.IsEquivalentTo(change(a)));
    }
}
