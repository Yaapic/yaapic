using Yaapic.App.Avalonia.Services;

namespace Yaapic.ViewModels.Tests;

public sealed class CaptureOptionsTests
{
    [Fact]
    public void No_arguments_leave_every_option_inert()
    {
        var o = CaptureOptions.Parse([]);

        Assert.Null(o.Workspace);
        Assert.Null(o.Theme);
        Assert.Null(o.Size);
        Assert.False(o.Send);
        Assert.Null(o.Select);
        Assert.Null(o.Open);
        Assert.Null(o.Screenshot);
        Assert.Equal(1500, o.WaitMs);
    }

    [Fact]
    public void All_options_are_parsed()
    {
        var o = CaptureOptions.Parse(["--workspace", "w", "--theme", "dark", "--size", "1440x900", "--send",
            "--select", "Body", "--open", "environments", "--screenshot", "a.png", "--wait-ms", "200"]);

        Assert.Equal("w", o.Workspace);
        Assert.Equal("dark", o.Theme);
        Assert.Equal((1440d, 900d), o.Size);
        Assert.True(o.Send);
        Assert.Equal("Body", o.Select);
        Assert.Equal("environments", o.Open);
        Assert.Equal("a.png", o.Screenshot);
        Assert.Equal(200, o.WaitMs);
    }

    [Fact]
    public void Unknown_options_and_bad_values_are_ignored()
    {
        var o = CaptureOptions.Parse(["--bogus", "--size", "big", "--wait-ms", "x", "--theme"]);

        Assert.Null(o.Size);
        Assert.Null(o.Theme);
        Assert.Equal(1500, o.WaitMs);
    }
}
