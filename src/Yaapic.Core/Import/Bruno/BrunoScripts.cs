using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Bruno;

/// <summary>Maps the scripts, <c>vars:*</c> and <c>tests</c> blocks of a Bruno request, folder or collection file.</summary>
/// <remarks>
/// Bruno collection and folder variables (the <c>vars:pre-request</c> pairs) are runtime variables set by script, so they
/// become generated <c>bru.setVar</c> lines, not <c>Variables</c> rows. The line numbers of the findings refer to the
/// generated source: the generated <c>vars</c> lines come first.
/// </remarks>
internal static class BrunoScripts
{
    /// <summary>Maps the script blocks of a parsed Bru file.</summary>
    /// <param name="file">The parsed file.</param>
    /// <param name="path">The report path of the owner of the file.</param>
    /// <param name="report">Receives the conversion findings.</param>
    /// <returns>The converted scripts, <see cref="ScriptSet.Empty"/> when there are none.</returns>
    internal static ScriptSet Map(BruFile file, string path, ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(report);

        var pre = Join(PreVars(file), file.Get("script:pre-request")?.Text);
        var post = Join(PostVars(file), file.Get("script:post-response")?.Text, file.Get("tests")?.Text);

        var set = ScriptSet.Empty;
        foreach (var (source, phase, name) in new[]
        {
            (pre, ScriptPhase.PreRequest, "pre-request"),
            (post, ScriptPhase.PostResponse, "post-response"),
        })
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                continue;
            }

            var converted = ScriptConverter.Convert(source, ScriptDialect.Bruno, ScriptLanguage.JavaScript);
            foreach (var f in converted.Findings)
            {
                report.Warning(path, Strings.ImportScriptFinding(path, name, f.Member, f.Line, f.Reason));
            }

            set = set.With(phase, new ScriptBlock(ScriptLanguage.JavaScript, converted.Text));
        }

        return set;
    }

    /// <summary>Counts the non-empty script blocks of a set.</summary>
    /// <param name="set">The scripts.</param>
    /// <returns>0, 1 or 2.</returns>
    internal static int Count(ScriptSet set) => (set.PreRequest.IsEmpty ? 0 : 1) + (set.PostResponse.IsEmpty ? 0 : 1);

    private static string PreVars(BruFile file) =>
        SetVars(file.Get("vars:pre-request"), value => DataNodeJson.Write(DataNode.String(value), false));

    private static string PostVars(BruFile file) => SetVars(file.Get("vars:post-response"), value => value);

    private static string SetVars(BruBlock? block, Func<string, string> value) =>
        string.Join('\n', (block?.Pairs ?? []).Where(p => p.Enabled).Select(p =>
            $"bru.setVar({DataNodeJson.Write(DataNode.String(p.Key), false)}, {value(p.Value)});"));

    private static string Join(params string?[] parts) =>
        string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
