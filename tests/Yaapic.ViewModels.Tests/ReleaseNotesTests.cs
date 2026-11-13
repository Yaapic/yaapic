using Yaapic.Localization;
using Yaapic.ViewModels.Updates;

namespace Yaapic.ViewModels.Tests;

public sealed class ReleaseNotesTests
{
    private const string Both = "<!-- lang:en -->\n## What's new\n\n- one\n\n<!-- lang:fr -->\n## Nouveautés\n\n- un\n";

    [Fact]
    public void Picks_the_section_of_the_language()
    {
        Assert.Equal("## Nouveautés\n\n- un", ReleaseNotes.For(Both, UiLanguage.French));
        Assert.Equal("## What's new\n\n- one", ReleaseNotes.For(Both, UiLanguage.English));
    }

    [Fact]
    public void Falls_back_to_english_then_to_the_first_section()
    {
        Assert.Equal("- one", ReleaseNotes.For("<!-- lang:en -->\n- one\n", UiLanguage.French));
        Assert.Equal("- un", ReleaseNotes.For("<!-- lang:fr -->\n- un\n", UiLanguage.English));
    }

    [Fact]
    public void Notes_without_markers_are_shown_whole_and_empty_stays_empty()
    {
        Assert.Equal("- plain\n", ReleaseNotes.For("- plain\n", UiLanguage.French));
        Assert.Equal("", ReleaseNotes.For("", UiLanguage.French));
    }

    [Fact]
    public void Handles_windows_line_endings()
    {
        Assert.Equal("- un", ReleaseNotes.For("<!-- lang:en -->\r\n- one\r\n<!-- lang:fr -->\r\n- un\r\n", UiLanguage.French));
    }
}
