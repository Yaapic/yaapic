using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.UI.Abstractions.Editing;
using static Yaapic.Scripting.Tests.Conformance.ConformanceRunner;

namespace Yaapic.Scripting.Tests.Conformance;

/// <summary>Checks that every member offered by the script completion exists on the real engines, so the two cannot drift apart.</summary>
public sealed class ScriptApiCatalogueTests
{
    private static IEnumerable<string> Paths(string prefix, IEnumerable<ScriptApiMember> members, bool lua)
    {
        foreach (var member in members.Where(m => m.ExistsIn(lua)))
        {
            var path = prefix.Length == 0 ? member.NameIn(lua) : $"{prefix}.{member.NameIn(lua)}";
            yield return path;
            if (member.IsChain)
            {
                // The chain is recursive; the words one level below the starting call are enough.
                foreach (var word in ScriptApiCatalogue.ChainMembers)
                {
                    yield return $"{path}(1).to.{word.NameIn(lua)}";
                }
            }
            else if (member.Members is { } children)
            {
                foreach (var child in Paths(path, children, lua))
                {
                    yield return child;
                }
            }
        }
    }

    private static List<string> AllPaths(bool lua) => [.. Paths("", ScriptApiCatalogue.TopLevel, lua).Distinct()];

    [Fact]
    public async Task Every_javascript_member_exists_on_the_engine()
    {
        var checks = string.Join('\n', AllPaths(lua: false).Select(p =>
            $"try {{ if (typeof ({p}) === 'undefined') missing.push('{p}'); }} catch (e) {{ /* a property assertion that failed: it exists */ }}"));
        var script = $"const missing = [];\n{checks}\npm.environment.set('missing', missing.join(','));";

        var (report, _) = await Run(new("catalogue-js"), post: script);

        Assert.Equal("", MissingOf(report));
    }

    [Fact]
    public async Task Every_lua_member_exists_on_the_engine()
    {
        var checks = string.Join('\n', AllPaths(lua: true).Select(p =>
            $"do local ok, v = pcall(function() return {p} end) if ok and v == nil then missing[#missing + 1] = '{p}' end end"));
        var script = $"local missing = {{}}\n{checks}\npm.environment.set('missing', table.concat(missing, ','))";

        var (report, _) = await Run(new("catalogue-lua") { Language = ScriptLanguage.Lua }, post: script);

        Assert.Equal("", MissingOf(report));
    }

    private static string MissingOf(ScriptedSendReport report) =>
        report.Changes.SingleOrDefault(c => c.Name == "missing")?.Value
        ?? $"no result: {string.Join(" | ", report.Console.Select(c => c.Message))} {string.Join(" | ", report.Runs.Select(r => r.Error))}";
}
