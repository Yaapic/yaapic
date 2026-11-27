using Yaapic.Domain.Monitors;

namespace Yaapic.Domain.Tests.Monitors;

public class MonitorDefinitionTests
{
    [Fact]
    public void Create_uses_the_defaults()
    {
        var folder = Guid.NewGuid();
        var m = MonitorDefinition.Create("Health", folder);
        Assert.Equal(folder, m.FolderId);
        Assert.Equal(60, m.IntervalMinutes);
        Assert.True(m.Enabled);
        Assert.Null(m.EnvironmentId);
        Assert.True(m.IsValid);
    }

    [Theory]
    [InlineData("", 60, false)]
    [InlineData("  ", 60, false)]
    [InlineData("x", 7, false)]
    [InlineData("x", 5, true)]
    [InlineData("x", 1440, true)]
    public void IsValid_checks_name_and_interval(string name, int interval, bool expected) =>
        Assert.Equal(expected, (MonitorDefinition.Create(name, Guid.NewGuid()) with { IntervalMinutes = interval }).IsValid);
}
