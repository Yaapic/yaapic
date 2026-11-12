using System.Text;

namespace Yaapic.Localization.Tests;

public class StringsGeneratorTests
{
    private static readonly string Dir = FindDir();

    private static string FindDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Yaapic.sln")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Yaapic.sln not found"), "src", "Yaapic.Localization");
    }

    private static string Read(string file) => File.ReadAllText(Path.Combine(Dir, file)).Replace("\r\n", "\n");

    [Fact]
    public void Strings_match_the_catalogue()
    {
        var actual = StringsGenerator.Generate(Read("strings.en.json"), Read("strings.fr.json"));
        var path = Path.Combine(Dir, "Strings.g.cs");
        if (Environment.GetEnvironmentVariable("YAAPIC_UPDATE_SNAPSHOTS") == "1")
        {
            File.WriteAllText(path, actual, new UTF8Encoding(false));
        }
        else
        {
            Assert.Equal(Read("Strings.g.cs"), actual);
        }
    }

    [Fact]
    public void French_has_no_key_that_English_lacks()
    {
        var english = StringsGenerator.Read(Read("strings.en.json"));
        var french = StringsGenerator.Read(Read("strings.fr.json"));

        Assert.Empty(french.Keys.Except(english.Keys));
    }

    [Fact]
    public void Every_english_key_is_translated()
    {
        var english = StringsGenerator.Read(Read("strings.en.json"));
        var french = StringsGenerator.Read(Read("strings.fr.json"));

        Assert.Empty(english.Keys.Except(french.Keys));
    }

    [Fact]
    public void Both_languages_use_the_same_placeholders()
    {
        var english = StringsGenerator.Read(Read("strings.en.json"));
        var french = StringsGenerator.Read(Read("strings.fr.json"));

        foreach (var (key, text) in french.Where(f => english.ContainsKey(f.Key)))
        {
            Assert.True(StringsGenerator.Placeholders(english[key]).SequenceEqual(StringsGenerator.Placeholders(text)), $"Placeholders differ for {key}.");
        }
    }

    [Fact]
    public void Two_keys_cannot_give_the_same_member() =>
        Assert.Throws<InvalidOperationException>(() => StringsGenerator.Generate("""{"a.b":"x","a-b":"y"}""", "{}"));

    [Fact]
    public void A_missing_french_text_falls_back_to_english()
    {
        var code = StringsGenerator.Generate("""{"a.b":"x"}""", "{}");

        Assert.Contains("Localizer.Pick(\"x\", null)", code, StringComparison.Ordinal);
    }
}
