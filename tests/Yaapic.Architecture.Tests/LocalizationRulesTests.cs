using System.Text.RegularExpressions;

namespace Yaapic.Architecture.Tests;

// A source-text scan, not a compiler check: it flags a literal that reads like an English sentence (a capitalised
// word followed by another word) in Yaapic.ViewModels and Yaapic.Core. User-visible text must come from Strings.
// Exception messages are developer-facing and stay English; comments and script samples (pm.) are skipped.
public partial class LocalizationRulesTests
{
    // Texts that are deliberately not translated: they are returned to scripts (script API).
    private static readonly string[] Allowed = ["Request failed"];

    [GeneratedRegex("""\$?"[A-Z][a-z]+ [A-Za-z.'{}]+""")]
    private static partial Regex Sentence();

    [Fact]
    public void View_model_and_core_texts_come_from_the_catalogue()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Yaapic.sln")))
            dir = dir.Parent;
        var src = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Yaapic.sln not found"), "src");

        var offenders = new List<string>();
        foreach (var project in new[] { "Yaapic.ViewModels", "Yaapic.Core" })
        {
            foreach (var file in Directory.GetFiles(Path.Combine(src, project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                        || line.Contains("Exception(", StringComparison.Ordinal)
                        || line.Contains("nameof(", StringComparison.Ordinal)
                        || line.Contains("pm.", StringComparison.Ordinal)
                        || Allowed.Any(a => line.Contains($"\"{a}\"", StringComparison.Ordinal)))
                        continue;

                    if (Sentence().IsMatch(line))
                        offenders.Add($"{Path.GetRelativePath(src, file)}:{i + 1}: {line.Trim()}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Hard-coded sentences (use Strings):\n" + string.Join('\n', offenders));
    }
}
