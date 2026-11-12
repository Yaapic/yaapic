using System.Text.RegularExpressions;

namespace Yaapic.Architecture.Tests;

// A source-text scan of the views of both front-ends: a user-visible attribute must bind to the catalogue
// ({x:Bind loc:Strings.X} on WinUI, {x:Static loc:Strings.X} on Avalonia), not hold an English literal.
// Generated theme dictionaries (Themes, Styles) and Assets are not views and are skipped.
public partial class XamlLiteralRulesTests
{
    // Literals that are deliberately not translated: product and protocol names, key names, code samples.
    private static readonly HashSet<string> Allowed =
    [
        "Yaapic", "&#x2022;", "Ctrl", "Enter", "Esc", "Ctrl+Enter", "Ctrl+S", "Ctrl+T", "Ctrl+Shift+F",
        "JSON", "XML", "HTML", "JavaScript", "API Key", "Basic Auth", "Bearer Token", "form-data", "x-www-form-urlencoded",
    ];

    [GeneratedRegex(""""(?<![\w.])(Content|Text|Header|Title|PlaceholderText|Watermark|ToolTipService\.ToolTip|ToolTip\.Tip|AutomationProperties\.Name|PrimaryButtonText|SecondaryButtonText|CloseButtonText|OnContent|OffContent)="([^"{][^"]*|\{\}[^"]*)"""")]
    private static partial Regex Literal();

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex Letter();

    [Fact]
    public void View_texts_come_from_the_catalogue()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Yaapic.sln")))
        {
            dir = dir.Parent;
        }

        var src = Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Yaapic.sln not found"), "src");
        var offenders = new List<string>();
        foreach (var (project, pattern) in new[] { ("Yaapic.App.WinUI", "*.xaml"), ("Yaapic.App.Avalonia", "*.axaml") })
        {
            foreach (var file in Directory.GetFiles(Path.Combine(src, project), pattern, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(src, file).Replace('\\', '/');
                if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/Themes/", StringComparison.Ordinal) || relative.Contains("/Styles/", StringComparison.Ordinal)
                    || relative.Contains("/Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    foreach (Match m in Literal().Matches(lines[i]))
                    {
                        var value = m.Groups[2].Value;
                        if (Letter().IsMatch(value) && !Allowed.Contains(value))
                        {
                            offenders.Add($"{relative}:{i + 1}: {m.Value}");
                        }
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0, "Hard-coded view texts (use Strings, or allow-list a name that must stay English):\n" + string.Join('\n', offenders));
    }
}
