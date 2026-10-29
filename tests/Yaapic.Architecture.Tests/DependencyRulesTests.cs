using System.Xml.Linq;

namespace Yaapic.Architecture.Tests;

// Rules are checked on the ProjectReference items of each csproj: an unused reference is not emitted
// into the compiled assembly, so assembly-level checks would miss it. Each card that adds a project adds
// its entry to AllowedReferences.
public class DependencyRulesTests
{
    private static readonly Dictionary<string, string[]> AllowedReferences = new()
    {
        ["Yaapic.Domain"] = [],
        ["Yaapic.Core"] = ["Yaapic.Domain"],
        ["Yaapic.UI.Abstractions"] = [],
        ["Yaapic.ViewModels"] = ["Yaapic.Core", "Yaapic.UI.Abstractions"],
    };

    private static string SrcDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Yaapic.sln")))
            dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Yaapic.sln not found"), "src");
    }

    private static Dictionary<string, string[]> ReadReferences() =>
        Directory.GetFiles(SrcDir(), "*.csproj", SearchOption.AllDirectories).ToDictionary(
            f => Path.GetFileNameWithoutExtension(f)!,
            f => XDocument.Load(f).Descendants("ProjectReference")
                .Select(r => Path.GetFileNameWithoutExtension(((string)r.Attribute("Include")!).Replace((char)92, '/')))
                .Order().ToArray());

    [Fact]
    public void Every_project_is_covered_by_a_rule()
    {
        var unknown = ReadReferences().Keys.Where(p => !AllowedReferences.ContainsKey(p) && !p.StartsWith("Yaapic.App."));
        Assert.Empty(unknown);
    }

    [Fact]
    public void Projects_only_reference_allowed_projects()
    {
        foreach (var (project, refs) in ReadReferences().Where(p => AllowedReferences.ContainsKey(p.Key)))
            Assert.Empty(refs.Except(AllowedReferences[project]));
    }

    [Fact]
    public void No_library_references_an_App_project()
    {
        var offenders = ReadReferences().Where(p => !p.Key.StartsWith("Yaapic.App.") && p.Value.Any(r => r.StartsWith("Yaapic.App.")));
        Assert.Empty(offenders);
    }
}
