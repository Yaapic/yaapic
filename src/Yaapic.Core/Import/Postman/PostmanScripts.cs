using System.Text;
using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Postman;

/// <summary>Maps the <c>event</c> scripts of a Postman collection, folder or request.</summary>
internal static class PostmanScripts
{
    /// <summary>Maps a Postman <c>event</c> array.</summary>
    /// <param name="events">The <c>event</c> node, possibly null.</param>
    /// <param name="path">The report path of the owner of the events.</param>
    /// <param name="report">Receives the skipped scripts and the conversion findings.</param>
    /// <returns>The converted scripts, <see cref="ScriptSet.Empty"/> when there are none.</returns>
    internal static ScriptSet Map(DataNode? events, string path, ImportReport report)
    {
        if (events is not { IsArray: true })
        {
            return ScriptSet.Empty;
        }

        var set = ScriptSet.Empty;
        foreach (var (listen, phase, name) in new[]
        {
            ("prerequest", ScriptPhase.PreRequest, "pre-request"),
            ("test", ScriptPhase.PostResponse, "post-response"),
        })
        {
            List<string> sources = [];
            foreach (var e in events.Items)
            {
                if (e.GetString("listen") == listen && e.GetBool("disabled") != true && Source(e["script"], path, report) is { } text)
                {
                    sources.Add(text);
                }
            }

            var joined = string.Join("\n\n", sources);
            if (string.IsNullOrWhiteSpace(joined))
            {
                continue;
            }

            var converted = ScriptConverter.Convert(joined, ScriptDialect.Postman, ScriptLanguage.JavaScript);
            foreach (var f in converted.Findings)
            {
                report.Warning(path, Strings.ImportScriptFinding(path, name, f.Member, f.Line, f.Reason));
            }

            set = set.With(phase, new ScriptBlock(ScriptLanguage.JavaScript, converted.Text));
        }

        return set;
    }

    private static string? Source(DataNode? script, string path, ImportReport report)
    {
        if (script is not { IsObject: true })
        {
            return null;
        }

        if (script.GetString("type") is { Length: > 0 } type && type != "text/javascript")
        {
            report.Skipped(path, Strings.ImportPostmanScriptType(path, type));
            return null;
        }

        var exec = script["exec"];
        if (exec is { IsArray: true })
        {
            StringBuilder sb = new();
            foreach (var line in exec.Items)
            {
                sb.Append(line.AsString()).Append('\n');
            }

            return sb.ToString().TrimEnd('\n');
        }

        return exec?.AsString();
    }
}
