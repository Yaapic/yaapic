using Yaapic.Design.Tokens;

namespace Yaapic.Design.Tokens.Tests;

public class TokenGeneratorTests
{
    private static readonly string DesignDir = FindDesignDir();

    private static string FindDesignDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Yaapic.sln")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Yaapic.sln not found"), "design");
    }

    // The reference files are checked out with the platform's line endings; the generator emits LF.
    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([DesignDir, .. path])).Replace("\r\n", "\n");

    private static string Tokens() => Read("tokens", "yaapic.tokens.json");

    [Fact]
    public void WinUI_output_matches_reference() =>
        Assert.Equal(Read("xaml", "Yaapic.Colors.WinUI.xaml"), TokenGenerator.GenerateWinUI(Tokens()));

    [Fact]
    public void Avalonia_output_matches_reference() =>
        Assert.Equal(Read("xaml", "Yaapic.Colors.Avalonia.axaml"), TokenGenerator.GenerateAvalonia(Tokens()));

    [Fact]
    public void Changing_a_token_changes_the_output_and_breaks_the_snapshot()
    {
        var changed = Tokens().Replace("#1FC7D4", "#FF00FF", StringComparison.Ordinal);

        var winUi = TokenGenerator.GenerateWinUI(changed);
        var avalonia = TokenGenerator.GenerateAvalonia(changed);

        Assert.Contains("#FFFF00FF", winUi);
        Assert.NotEqual(Read("xaml", "Yaapic.Colors.WinUI.xaml"), winUi);
        Assert.NotEqual(Read("xaml", "Yaapic.Colors.Avalonia.axaml"), avalonia);
    }

    [Theory]
    [InlineData("#1FC7D4", "#FF1FC7D4")]
    [InlineData("#1FC7D41A", "#1A1FC7D4")]
    public void Colours_are_converted_to_argb(string dtcg, string argb)
    {
        var json = Tokens().Replace("#131518", dtcg, StringComparison.Ordinal);
        Assert.Contains($"YaapicSurfaceAppBrush\" Color=\"{argb}\"", TokenGenerator.GenerateWinUI(json));
    }
}
